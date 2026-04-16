using LocalWallet.Models;
using LocalWallet.Services.Database;
using NSec.Cryptography;

namespace LocalWallet.Services.Crypto;

public class DeviceIdentityService : IDeviceIdentityService, IDisposable
{
    private const string PrivateKeyStorageKey = "lw.device.ed25519.priv";
    private static readonly SignatureAlgorithm Algo = SignatureAlgorithm.Ed25519;

    private readonly IDatabaseService _db;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private Key? _key;
    private byte[] _publicKeyBytes = Array.Empty<byte>();
    private string _deviceId = string.Empty;
    private string _displayName = "Моё устройство";
    private bool _initialized;

    public DeviceIdentityService(IDatabaseService db)
    {
        _db = db;
    }

    public string DeviceId => _deviceId;
    public string DisplayName => _displayName;
    public byte[] PublicKeyBytes => _publicKeyBytes;

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            string? storedSeedB64 = null;
            try { storedSeedB64 = await SecureStorage.Default.GetAsync(PrivateKeyStorageKey); }
            catch { storedSeedB64 = null; }

            if (!string.IsNullOrEmpty(storedSeedB64))
            {
                try
                {
                    var seed = Convert.FromBase64String(storedSeedB64);
                    _key = Key.Import(Algo, seed, KeyBlobFormat.RawPrivateKey,
                        new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
                }
                catch
                {
                    _key = null;
                }
            }

            if (_key is null)
            {
                _key = Key.Create(Algo, new KeyCreationParameters
                {
                    ExportPolicy = KeyExportPolicies.AllowPlaintextExport
                });
                var seed = _key.Export(KeyBlobFormat.RawPrivateKey);
                try { await SecureStorage.Default.SetAsync(PrivateKeyStorageKey, Convert.ToBase64String(seed)); }
                catch { /* SecureStorage unavailable on some dev setups */ }
            }

            _publicKeyBytes = _key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
            _deviceId = Convert.ToBase64String(_publicKeyBytes);

            var identity = await _db.GetDeviceIdentityAsync();
            if (identity is null)
            {
                identity = new DeviceIdentity
                {
                    Id = 1,
                    DeviceId = _deviceId,
                    DisplayName = _displayName,
                    CreatedAt = DateTime.UtcNow
                };
                await _db.SaveDeviceIdentityAsync(identity);
            }
            else
            {
                if (!string.Equals(identity.DeviceId, _deviceId, StringComparison.Ordinal))
                {
                    identity.DeviceId = _deviceId;
                    identity.CreatedAt = identity.CreatedAt == default ? DateTime.UtcNow : identity.CreatedAt;
                    await _db.SaveDeviceIdentityAsync(identity);
                }
                _displayName = string.IsNullOrWhiteSpace(identity.DisplayName) ? _displayName : identity.DisplayName;
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task SetDisplayNameAsync(string name)
    {
        if (!_initialized) await InitializeAsync();
        var trimmed = string.IsNullOrWhiteSpace(name) ? "Моё устройство" : name.Trim();
        _displayName = trimmed;
        var identity = await _db.GetDeviceIdentityAsync();
        if (identity is null)
        {
            identity = new DeviceIdentity
            {
                Id = 1,
                DeviceId = _deviceId,
                DisplayName = trimmed,
                CreatedAt = DateTime.UtcNow
            };
        }
        else
        {
            identity.DisplayName = trimmed;
        }
        await _db.SaveDeviceIdentityAsync(identity);
    }

    public byte[] Sign(ReadOnlySpan<byte> data)
    {
        if (_key is null) throw new InvalidOperationException("DeviceIdentityService not initialized");
        return Algo.Sign(_key, data);
    }

    public bool Verify(byte[] publicKey, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        try
        {
            var pk = PublicKey.Import(Algo, publicKey, KeyBlobFormat.RawPublicKey);
            return Algo.Verify(pk, data, signature);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _key?.Dispose();
        _initLock.Dispose();
    }
}
