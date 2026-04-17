using System.Security.Cryptography;
using LocalWallet.Models;
using LocalWallet.Services.Database;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace LocalWallet.Services.Crypto;

public class DeviceIdentityService : IDeviceIdentityService, IDisposable
{
    private const string PrivateKeyStorageKey = "lw.device.ed25519.priv";

    private readonly IDatabaseService _db;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private Ed25519PrivateKeyParameters? _priv;
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
                    if (seed.Length == Ed25519PrivateKeyParameters.KeySize)
                        _priv = new Ed25519PrivateKeyParameters(seed, 0);
                }
                catch
                {
                    _priv = null;
                }
            }

            if (_priv is null)
            {
                var seed = new byte[Ed25519PrivateKeyParameters.KeySize];
                RandomNumberGenerator.Fill(seed);
                _priv = new Ed25519PrivateKeyParameters(seed, 0);
                try { await SecureStorage.Default.SetAsync(PrivateKeyStorageKey, Convert.ToBase64String(seed)); }
                catch { /* SecureStorage unavailable on some dev setups */ }
            }

            _publicKeyBytes = _priv.GeneratePublicKey().GetEncoded();
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
        if (_priv is null) throw new InvalidOperationException("DeviceIdentityService not initialized");
        var signer = new Ed25519Signer();
        signer.Init(true, _priv);
        var buf = data.ToArray();
        signer.BlockUpdate(buf, 0, buf.Length);
        return signer.GenerateSignature();
    }

    public bool Verify(byte[] publicKey, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        try
        {
            if (publicKey.Length != Ed25519PublicKeyParameters.KeySize) return false;
            var pub = new Ed25519PublicKeyParameters(publicKey, 0);
            var signer = new Ed25519Signer();
            signer.Init(false, pub);
            var buf = data.ToArray();
            signer.BlockUpdate(buf, 0, buf.Length);
            return signer.VerifySignature(signature.ToArray());
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _initLock.Dispose();
    }
}
