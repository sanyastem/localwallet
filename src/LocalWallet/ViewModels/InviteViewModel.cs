using System.Net.Sockets;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Services;
using LocalWallet.Services.Crypto;
using LocalWallet.Services.Database;
using LocalWallet.Services.Families;
using LocalWallet.Services.Sync;
using LocalWallet.ViewModels.Base;
using QRCoder;

namespace LocalWallet.ViewModels;

[QueryProperty(nameof(FamilyIdString), "id")]
public partial class InviteViewModel : BaseViewModel
{
    private readonly IFamilyService _families;
    private readonly IDatabaseService _db;
    private readonly IDeviceIdentityService _identity;
    private readonly IPairingService _pairing;
    private readonly IFamilyCryptoService _crypto;

    [ObservableProperty] private string familyIdString = string.Empty;
    [ObservableProperty] private Models.Family? family;
    [ObservableProperty] private ImageSource? qrImage;
    [ObservableProperty] private string sas = string.Empty;
    [ObservableProperty] private string status = "Подготовка…";
    [ObservableProperty] private string peerDeviceName = string.Empty;
    [ObservableProperty] private bool pairingInProgress;
    [ObservableProperty] private bool sasVisible;
    [ObservableProperty] private string hostInfo = string.Empty;

    private PairingHostSession? _session;
    private CancellationTokenSource? _cts;
    private string? _peerDeviceId;
    private string? _peerDisplayName;

    public InviteViewModel(
        IFamilyService families,
        IDatabaseService db,
        IDeviceIdentityService identity,
        IPairingService pairing,
        IFamilyCryptoService crypto)
    {
        _families = families;
        _db = db;
        _identity = identity;
        _pairing = pairing;
        _crypto = crypto;
        Title = "Пригласить";
    }

    [RelayCommand]
    public async Task StartAsync()
    {
        try
        {
            if (!Guid.TryParse(FamilyIdString, out var fid)) return;
            try { await _identity.InitializeAsync(); } catch { }
            Family = await _db.GetFamilyAsync(fid);
            if (Family is null) { Status = "Семья не найдена"; return; }

            var eph = _pairing.CreateEphemeralKeypair();
            var salt = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(salt);

            var listener = new TcpListener(System.Net.IPAddress.Any, 0);
            listener.Start();
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            var ip = LocalNetwork.GetLocalIPv4() ?? "0.0.0.0";

            _session = new PairingHostSession
            {
                Ours = eph,
                OurSalt = salt,
                Listener = listener,
                Port = port,
                LocalIp = ip
            };

            HostInfo = $"{ip}:{port}";

            var payload = new InvitationPayload(
                V: InvitationCodec.Version,
                FamilyId: Family.Id.ToString(),
                FamilyName: Family.Name,
                DeviceId: _identity.DeviceId,
                EphemeralPub: Convert.ToBase64String(eph.PublicKey),
                Ip: ip,
                Port: port,
                SaltHex: Convert.ToHexString(salt));

            QrImage = GenerateQr(InvitationCodec.Encode(payload));

            Status = "Попросите другое устройство отсканировать QR";

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _ = Task.Run(async () =>
            {
                try { await AcceptPeerAsync(token); }
                catch { /* top-level guard */ }
            });
        }
        catch (Exception ex)
        {
            Status = "Ошибка: " + ex.Message;
        }
    }

    private async Task AcceptPeerAsync(CancellationToken ct)
    {
        if (_session is null) return;
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() => Status = "Ожидание подключения…");
            var client = await _session.Listener.AcceptTcpClientAsync(ct);
            _session.Client = client;
            var stream = client.GetStream();
            _session.Stream = stream;

            var helloRaw = await SyncTransport.ReadFrameAsync(stream, ct);
            if (helloRaw is null) throw new Exception("нет hello");
            var hello = JsonSerializer.Deserialize<PairingHostMessages.Hello>(
                System.Text.Encoding.UTF8.GetString(helloRaw))
                ?? throw new Exception("плохой hello");

            _peerDeviceId = hello.DeviceId;
            _peerDisplayName = hello.DisplayName;
            var peerPub = Convert.FromBase64String(hello.EphemeralPubBase64);
            var peerSalt = Convert.FromBase64String(hello.SaltBase64);

            var combined = new byte[_session.OurSalt.Length + peerSalt.Length];
            Buffer.BlockCopy(_session.OurSalt, 0, combined, 0, _session.OurSalt.Length);
            Buffer.BlockCopy(peerSalt, 0, combined, _session.OurSalt.Length, peerSalt.Length);

            var result = _pairing.DeriveSession(_session.Ours, peerPub, combined);
            _session.SessionKey = result.SessionKey;
            _session.Sas = result.Sas;

            var hostReply = new PairingHostMessages.Hello
            {
                DeviceId = _identity.DeviceId,
                DisplayName = _identity.DisplayName,
                EphemeralPubBase64 = Convert.ToBase64String(_session.Ours.PublicKey),
                SaltBase64 = Convert.ToBase64String(_session.OurSalt)
            };
            await SendJsonAsync(stream, hostReply, ct);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Sas = result.Sas;
                SasVisible = true;
                PeerDeviceName = _peerDisplayName ?? "неизвестное устройство";
                Status = $"Подключено устройство «{PeerDeviceName}». Сверьте код с ним и подтвердите.";
                PairingInProgress = true;
            });
        }
        catch (OperationCanceledException) { /* expected on cancel */ }
        catch (Exception ex)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() => Status = "Ошибка соединения: " + ex.Message);
            }
            catch { }
            Cleanup();
        }
    }

    [RelayCommand]
    private async Task ConfirmSasAsync()
    {
        if (_session?.Stream is null || _session.SessionKey is null || Family is null) return;

        try
        {
            var familyKey = await _families.GetFamilyKeyAsync(Family.Id)
                ?? throw new Exception("нет family key");

            var bundle = new PairingHostMessages.FamilyBundle
            {
                FamilyId = Family.Id.ToString(),
                FamilyName = Family.Name,
                FamilyKeyBase64 = Convert.ToBase64String(familyKey),
                OwnerDeviceId = _identity.DeviceId,
                OwnerDisplayName = _identity.DisplayName
            };
            var plaintext = JsonSerializer.SerializeToUtf8Bytes(bundle);
            var env = _crypto.Seal(_session.SessionKey, plaintext);
            var msg = new PairingHostMessages.Sealed
            {
                NonceBase64 = Convert.ToBase64String(env.Nonce),
                CiphertextBase64 = Convert.ToBase64String(env.CiphertextAndTag)
            };

            await SendJsonAsync(_session.Stream, msg, CancellationToken.None);

            if (!string.IsNullOrWhiteSpace(_peerDeviceId))
                await _families.UpsertMemberAsync(Family.Id, _peerDeviceId, _peerDisplayName ?? "", "Member");

            Status = "Готово. Второе устройство присоединилось.";
            SasVisible = false;
            PairingInProgress = false;
            Cleanup();

            await UiAlerts.ShowAsync("Успех", $"«{_peerDisplayName}» присоединился к семье.");
            try { if (Shell.Current is not null) await Shell.Current.GoToAsync(".."); } catch { }
        }
        catch (Exception ex)
        {
            Status = "Ошибка отправки: " + ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        Status = "Отменено.";
        Cleanup();
    }

    private void Cleanup()
    {
        try { _cts?.Cancel(); } catch { }
        try { _session?.Stream?.Dispose(); } catch { }
        try { _session?.Client?.Dispose(); } catch { }
        try { _session?.Listener?.Stop(); } catch { }
        try { _session?.Ours.Clear(); } catch { }
        _session = null;
    }

    private static ImageSource GenerateQr(string content)
    {
        using var gen = new QRCodeGenerator();
        var data = gen.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        var bytes = new PngByteQRCode(data).GetGraphic(12);
        return ImageSource.FromStream(() => new MemoryStream(bytes));
    }

    private static async Task SendJsonAsync<T>(NetworkStream stream, T payload, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await SyncTransport.WriteFrameAsync(stream, bytes, ct);
    }
}
