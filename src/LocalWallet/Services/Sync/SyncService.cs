using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LocalWallet.Services.Crypto;
using LocalWallet.Services.Database;

namespace LocalWallet.Services.Sync;

public class SyncService : ISyncService
{
    private const int DefaultPort = 47321;

    private readonly IDatabaseService _db;
    private readonly IDeviceIdentityService _identity;
    private readonly IEventStore _events;
    private readonly IFamilyCryptoService _crypto;
    private readonly Family.IFamilyService _families;

    private TcpListener? _listener;
    private CancellationTokenSource? _listenerCts;
    private Task? _acceptTask;

    public SyncService(
        IDatabaseService db,
        IDeviceIdentityService identity,
        IEventStore events,
        IFamilyCryptoService crypto,
        Family.IFamilyService families)
    {
        _db = db;
        _identity = identity;
        _events = events;
        _crypto = crypto;
        _families = families;
    }

    public bool IsListening => _listener is not null;
    public int? ListenPort { get; private set; }

    public Task<int> StartListenerAsync(Guid? inviteFamilyId = null, CancellationToken ct = default)
    {
        StopListener();
        _listenerCts = new CancellationTokenSource();

        var listener = new TcpListener(IPAddress.Any, 0);
        listener.Start();
        _listener = listener;
        ListenPort = ((IPEndPoint)listener.LocalEndpoint).Port;

        var token = _listenerCts.Token;
        _acceptTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync(token);
                    _ = Task.Run(() => HandleIncomingAsync(client, token));
                }
                catch
                {
                    break;
                }
            }
        }, token);

        return Task.FromResult(ListenPort.Value);
    }

    public void StopListener()
    {
        try { _listenerCts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
        _listener = null;
        _listenerCts = null;
        _acceptTask = null;
        ListenPort = null;
    }

    private async Task HandleIncomingAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            var stream = client.GetStream();
            try
            {
                await PerformSyncAsync(stream, asServer: true, ct);
            }
            catch { /* tolerated */ }
        }
    }

    public async Task<SyncResult> SyncWithPeerAsync(Guid familyId, string host, int port, CancellationToken ct = default)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, ct);
            var stream = client.GetStream();
            return await PerformSyncAsync(stream, asServer: false, ct, familyId);
        }
        catch (Exception ex)
        {
            return new SyncResult(false, 0, 0, ex.Message);
        }
    }

    private async Task<SyncResult> PerformSyncAsync(NetworkStream stream, bool asServer, CancellationToken ct, Guid? clientFamilyId = null)
    {
        await _identity.InitializeAsync();

        Guid familyId;
        string peerDeviceId;
        string peerDisplayName;

        if (asServer)
        {
            var helloRaw = await SyncTransport.ReadFrameAsync(stream, ct);
            if (helloRaw is null) return new SyncResult(false, 0, 0, "no hello");
            var (_, helloJson) = UnwrapTyped(helloRaw);
            var hello = JsonSerializer.Deserialize<SyncHello>(helloJson);
            if (hello is null || !Guid.TryParse(hello.FamilyId, out familyId)) return new SyncResult(false, 0, 0, "bad hello");

            peerDeviceId = hello.FromDeviceId;
            peerDisplayName = hello.FromDisplayName;

            var family = await _db.GetFamilyAsync(familyId);
            if (family is null) return new SyncResult(false, 0, 0, "family unknown");
            var member = await _db.FindFamilyMemberAsync(familyId, peerDeviceId);
            if (member is null) return new SyncResult(false, 0, 0, "peer not a member");

            var ourChallenge = Convert.ToBase64String(RandomBytes(32));
            var sigOfTheirChallenge = _identity.Sign(Encoding.UTF8.GetBytes(hello.Challenge));

            var ack = new SyncHelloAck
            {
                FromDeviceId = _identity.DeviceId,
                FromDisplayName = _identity.DisplayName,
                ChallengeSignatureBase64 = Convert.ToBase64String(sigOfTheirChallenge),
                OurChallenge = ourChallenge,
                FromPublicKeyBase64 = _identity.DeviceId
            };
            await SendTypedAsync(stream, SyncMessageType.HelloAck, ack, ct);

            var peerProof = await SyncTransport.ReadFrameAsync(stream, ct);
            if (peerProof is null) return new SyncResult(false, 0, 0, "no proof");
            var (_, proofJson) = UnwrapTyped(peerProof);
            var proofDict = JsonSerializer.Deserialize<Dictionary<string, string>>(proofJson) ?? new();
            var proofSig = Convert.FromBase64String(proofDict.GetValueOrDefault("sig") ?? "");
            var peerPub = Convert.FromBase64String(peerDeviceId);
            if (!_identity.Verify(peerPub, Encoding.UTF8.GetBytes(ourChallenge), proofSig))
                return new SyncResult(false, 0, 0, "bad peer signature");
        }
        else
        {
            familyId = clientFamilyId!.Value;
            var family = await _db.GetFamilyAsync(familyId);
            if (family is null) return new SyncResult(false, 0, 0, "no family");

            var ourChallenge = Convert.ToBase64String(RandomBytes(32));
            var hello = new SyncHello
            {
                FromDeviceId = _identity.DeviceId,
                FromDisplayName = _identity.DisplayName,
                FamilyId = familyId.ToString(),
                Challenge = ourChallenge
            };
            await SendTypedAsync(stream, SyncMessageType.Hello, hello, ct);

            var ackRaw = await SyncTransport.ReadFrameAsync(stream, ct);
            if (ackRaw is null) return new SyncResult(false, 0, 0, "no ack");
            var (_, ackJson) = UnwrapTyped(ackRaw);
            var ack = JsonSerializer.Deserialize<SyncHelloAck>(ackJson);
            if (ack is null) return new SyncResult(false, 0, 0, "bad ack");
            peerDeviceId = ack.FromDeviceId;
            peerDisplayName = ack.FromDisplayName;

            var peerPub = Convert.FromBase64String(peerDeviceId);
            var sig = Convert.FromBase64String(ack.ChallengeSignatureBase64);
            if (!_identity.Verify(peerPub, Encoding.UTF8.GetBytes(ourChallenge), sig))
                return new SyncResult(false, 0, 0, "bad server signature");

            var mySig = _identity.Sign(Encoding.UTF8.GetBytes(ack.OurChallenge));
            await SendTypedAsync(stream, SyncMessageType.VectorClock, new Dictionary<string, string>
            {
                ["sig"] = Convert.ToBase64String(mySig)
            }, ct);
        }

        await _families.UpsertMemberAsync(familyId, peerDeviceId, peerDisplayName);

        var familyKey = await _families.GetFamilyKeyAsync(familyId);
        if (familyKey is null) return new SyncResult(false, 0, 0, "no family key");

        var ourClock = (await _db.GetVectorClockAsync(familyId))
            .ToDictionary(x => x.DeviceId, x => x.MaxClock);
        await SendTypedAsync(stream, SyncMessageType.VectorClock, new VectorClockMessage { Clocks = ourClock }, ct);

        var peerClockRaw = await SyncTransport.ReadFrameAsync(stream, ct);
        if (peerClockRaw is null) return new SyncResult(false, 0, 0, "no peer clock");
        var (_, peerClockJson) = UnwrapTyped(peerClockRaw);
        var peerVector = JsonSerializer.Deserialize<VectorClockMessage>(peerClockJson)?.Clocks ?? new();

        var eventsToSend = new List<SyncEventWire>();
        foreach (var (did, peerMax) in peerVector.Concat(new[] { new KeyValuePair<string, long>(_identity.DeviceId, 0) }))
        {
            var ourMax = ourClock.GetValueOrDefault(did, 0);
            if (ourMax <= peerMax) continue;
            var missing = await _db.GetEventsSinceAsync(familyId, did, peerMax);
            foreach (var e in missing) eventsToSend.Add(SyncEventWire.From(e));
        }
        foreach (var did in ourClock.Keys)
        {
            if (peerVector.ContainsKey(did)) continue;
            var all = await _db.GetEventsSinceAsync(familyId, did, 0);
            foreach (var e in all) eventsToSend.Add(SyncEventWire.From(e));
        }

        await SendTypedAsync(stream, SyncMessageType.EventBatch, new EventBatchMessage { Events = eventsToSend }, ct);

        var peerBatchRaw = await SyncTransport.ReadFrameAsync(stream, ct);
        if (peerBatchRaw is null) return new SyncResult(false, eventsToSend.Count, 0, "no peer batch");
        var (_, peerBatchJson) = UnwrapTyped(peerBatchRaw);
        var peerBatch = JsonSerializer.Deserialize<EventBatchMessage>(peerBatchJson)?.Events ?? new();

        var accepted = 0;
        foreach (var w in peerBatch.OrderBy(e => e.LamportClock))
        {
            var ev = w.ToEvent();
            byte[] authorPub;
            try { authorPub = Convert.FromBase64String(ev.AuthorDeviceId); }
            catch { continue; }
            if (await _events.AcceptRemoteAsync(ev, familyKey, authorPub)) accepted++;
        }

        var f = await _db.GetFamilyAsync(familyId);
        if (f is not null)
        {
            f.LastSyncedAt = DateTime.UtcNow;
            await _db.SaveFamilyAsync(f);
        }

        return new SyncResult(true, eventsToSend.Count, accepted);
    }

    private static byte[] RandomBytes(int n)
    {
        var b = new byte[n];
        RandomNumberGenerator.Fill(b);
        return b;
    }

    private static async Task SendTypedAsync<T>(NetworkStream stream, SyncMessageType type, T payload, CancellationToken ct)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(payload);
        var frame = new byte[body.Length + 1];
        frame[0] = (byte)type;
        Buffer.BlockCopy(body, 0, frame, 1, body.Length);
        await SyncTransport.WriteFrameAsync(stream, frame, ct);
    }

    private static (SyncMessageType Type, string Json) UnwrapTyped(byte[] frame)
    {
        if (frame.Length < 1) return (SyncMessageType.Error, "{}");
        var type = (SyncMessageType)frame[0];
        var json = Encoding.UTF8.GetString(frame, 1, frame.Length - 1);
        return (type, json);
    }

    public static int SuggestedPort() => DefaultPort;
}
