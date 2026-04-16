using System.Net.Sockets;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Services.Crypto;
using LocalWallet.Services.Families;
using LocalWallet.Services.Sync;
using LocalWallet.ViewModels.Base;

namespace LocalWallet.ViewModels;

public partial class JoinFamilyViewModel : BaseViewModel
{
    private readonly IFamilyService _families;
    private readonly IDeviceIdentityService _identity;
    private readonly IPairingService _pairing;
    private readonly IFamilyCryptoService _crypto;

    [ObservableProperty] private string inviteJson = string.Empty;
    [ObservableProperty] private string status = "Вставьте JSON приглашения из QR-кода";
    [ObservableProperty] private string sas = string.Empty;
    [ObservableProperty] private string peerDisplayName = string.Empty;
    [ObservableProperty] private bool sasVisible;

    private InvitationPayload? _invite;
    private PairingClientSession? _session;

    public JoinFamilyViewModel(
        IFamilyService families,
        IDeviceIdentityService identity,
        IPairingService pairing,
        IFamilyCryptoService crypto)
    {
        _families = families;
        _identity = identity;
        _pairing = pairing;
        _crypto = crypto;
        Title = "Присоединиться";
    }

    [RelayCommand]
    private async Task PasteAsync()
    {
        try
        {
            var text = await Clipboard.Default.GetTextAsync();
            if (!string.IsNullOrWhiteSpace(text)) InviteJson = text;
        }
        catch { }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (_invite is not null || IsBusy) return;
        if (string.IsNullOrWhiteSpace(InviteJson))
        {
            Status = "Вставьте JSON приглашения";
            return;
        }

        var payload = InvitationCodec.Decode(InviteJson.Trim());
        if (payload is null)
        {
            Status = "Это не JSON приглашения LocalWallet";
            return;
        }

        _invite = payload;
        IsBusy = true;
        Status = $"Подключение к {payload.FamilyName} ({payload.Ip}:{payload.Port})…";

        await _identity.InitializeAsync();

        try
        {
            var eph = _pairing.CreateEphemeralKeypair();
            var mySalt = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(mySalt);

            _session = new PairingClientSession { Ours = eph, OurSalt = mySalt };
            var client = new TcpClient();
            await client.ConnectAsync(payload.Ip, payload.Port);
            _session.Client = client;
            var stream = client.GetStream();
            _session.Stream = stream;

            var hello = new PairingHostMessages.Hello
            {
                DeviceId = _identity.DeviceId,
                DisplayName = _identity.DisplayName,
                EphemeralPubBase64 = Convert.ToBase64String(eph.PublicKey),
                SaltBase64 = Convert.ToBase64String(mySalt)
            };
            await SendJsonAsync(stream, hello);

            var hostHelloRaw = await SyncTransport.ReadFrameAsync(stream);
            if (hostHelloRaw is null) throw new Exception("нет ответа");
            var hostHello = JsonSerializer.Deserialize<PairingHostMessages.Hello>(
                System.Text.Encoding.UTF8.GetString(hostHelloRaw))
                ?? throw new Exception("плохой ответ");

            var peerPub = Convert.FromBase64String(hostHello.EphemeralPubBase64);
            var peerSalt = Convert.FromBase64String(hostHello.SaltBase64);
            PeerDisplayName = hostHello.DisplayName;

            var combined = new byte[mySalt.Length + peerSalt.Length];
            Buffer.BlockCopy(mySalt, 0, combined, 0, mySalt.Length);
            Buffer.BlockCopy(peerSalt, 0, combined, mySalt.Length, peerSalt.Length);

            var result = _pairing.DeriveSession(eph, peerPub, combined);
            _session.SessionKey = result.SessionKey;
            _session.Sas = result.Sas;

            Sas = result.Sas;
            SasVisible = true;
            Status = $"Сверьте код на устройстве {hostHello.DisplayName} и подтвердите.";
        }
        catch (Exception ex)
        {
            Status = "Ошибка: " + ex.Message;
            Reset();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmSasAsync()
    {
        if (_session?.Stream is null || _session.SessionKey is null || _invite is null) return;
        try
        {
            var sealedMsgRaw = await SyncTransport.ReadFrameAsync(_session.Stream);
            if (sealedMsgRaw is null) throw new Exception("нет пакета");
            var sealedMsg = JsonSerializer.Deserialize<PairingHostMessages.Sealed>(
                System.Text.Encoding.UTF8.GetString(sealedMsgRaw))
                ?? throw new Exception("плохой пакет");

            var plaintext = _crypto.Open(_session.SessionKey,
                new AeadEnvelope(
                    Convert.FromBase64String(sealedMsg.NonceBase64),
                    Convert.FromBase64String(sealedMsg.CiphertextBase64)));
            if (plaintext is null) throw new Exception("не удалось расшифровать");

            var bundle = JsonSerializer.Deserialize<PairingHostMessages.FamilyBundle>(plaintext)
                ?? throw new Exception("плохая посылка");

            if (!Guid.TryParse(bundle.FamilyId, out var fid)) throw new Exception("bad family id");
            var familyKey = Convert.FromBase64String(bundle.FamilyKeyBase64);
            await _families.JoinAsync(fid, bundle.FamilyName, familyKey, bundle.OwnerDeviceId, bundle.OwnerDisplayName);

            Status = $"Вы присоединились к семье «{bundle.FamilyName}».";
            SasVisible = false;
            Reset();

            if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page is not null)
                await Application.Current.Windows[0].Page!.DisplayAlertAsync("Готово", $"Семья «{bundle.FamilyName}» добавлена.", "OK");
            await Shell.Current.GoToAsync($"..?id={fid}");
        }
        catch (Exception ex)
        {
            Status = "Ошибка: " + ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        Reset();
        Status = "Отменено. Вставьте новое приглашение.";
    }

    private void Reset()
    {
        try { _session?.Stream?.Dispose(); } catch { }
        try { _session?.Client?.Dispose(); } catch { }
        _session?.Ours.Clear();
        _session = null;
        _invite = null;
    }

    private static async Task SendJsonAsync<T>(NetworkStream stream, T payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await SyncTransport.WriteFrameAsync(stream, bytes);
    }
}
