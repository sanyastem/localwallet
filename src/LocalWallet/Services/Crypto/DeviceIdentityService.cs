using LocalWallet.Models;
using LocalWallet.Services.Database;
using NSec.Cryptography;

namespace LocalWallet.Services.Crypto;

public class DeviceIdentityService : IDeviceIdentityService, IDisposable
{
    private const string PrivateKeyStorageKey = "lw.device.ed25519.priv";

    // Deliberately lazy: touching NSec static state at type-load time used to throw
    // TypeInitializationException on devices where libsodium can't be loaded, which
    // crashed DI resolution and black-screened every page that transitively needed
    // this service.
    private static readonly Lazy<SignatureAlgorithm?> AlgoLazy = new(() =>
    {
        try { return SignatureAlgorithm.Ed25519; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeviceIdentity] NSec unavailable: {ex}");
            return null;
        }
    });
    private static SignatureAlgorithm? Algo => AlgoLazy.Value;

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

            var algo = Algo;
            if (algo is not null)
            {
                try { await InitializeWithCryptoAsync(algo); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DeviceIdentity] crypto init failed: {ex}");
                    await InitializeWithoutCryptoAsync();
                }
            }
            else
            {
                await InitializeWithoutCryptoAsync();
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task InitializeWithCryptoAsync(SignatureAlgorithm algo)
    {
        string? storedSeedB64 = null;
        try { storedSeedB64 = await SecureStorage.Default.GetAsync(PrivateKeyStorageKey); }
        catch { storedSeedB64 = null; }

        if (!string.IsNullOrEmpty(storedSeedB64))
        {
            try
            {
                var seed = Convert.FromBase64String(storedSeedB64);
                _key = Key.Import(algo, seed, KeyBlobFormat.RawPrivateKey,
                    new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
            }
            catch
            {
                _key = null;
            }
        }

        if (_key is null)
        {
            _key = Key.Create(algo, new KeyCreationParameters
            {
                ExportPolicy = KeyExportPolicies.AllowPlaintextExport
            });
            var seed = _key.Export(KeyBlobFormat.RawPrivateKey);
            try { await SecureStorage.Default.SetAsync(PrivateKeyStorageKey, Convert.ToBase64String(seed)); }
            catch { /* SecureStorage unavailable on some dev setups */ }
        }

        _publicKeyBytes = _key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        _deviceId = Convert.ToBase64String(_publicKeyBytes);

        await PersistIdentityAsync();
    }

    private async Task InitializeWithoutCryptoAsync()
    {
        // NSec isn't available on this device. Fall back to a synthetic device id so
        // basic wallet features keep working; family sync will refuse to sign/verify
        // later but won't crash DI.
        var identity = await _db.GetDeviceIdentityAsync();
        if (identity is not null && !string.IsNullOrWhiteSpace(identity.DeviceId))
        {
            _deviceId = identity.DeviceId;
            _displayName = string.IsNullOrWhiteSpace(identity.DisplayName) ? _displayName : identity.DisplayName;
            return;
        }

        _deviceId = "local-" + Guid.NewGuid().ToString("N");
        await _db.SaveDeviceIdentityAsync(new DeviceIdentity
        {
            Id = 1,
            DeviceId = _deviceId,
            DisplayName = _displayName,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task PersistIdentityAsync()
    {
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
        var algo = Algo;
        if (algo is null || _key is null)
            throw new InvalidOperationException("DeviceIdentityService: crypto unavailable");
        return algo.Sign(_key, data);
    }

    public bool Verify(byte[] publicKey, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        var algo = Algo;
        if (algo is null) return false;
        try
        {
            var pk = PublicKey.Import(algo, publicKey, KeyBlobFormat.RawPublicKey);
            return algo.Verify(pk, data, signature);
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
