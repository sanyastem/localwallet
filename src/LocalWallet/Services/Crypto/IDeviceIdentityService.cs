namespace LocalWallet.Services.Crypto;

public interface IDeviceIdentityService
{
    Task InitializeAsync();

    string DeviceId { get; }

    string DisplayName { get; }

    byte[] PublicKeyBytes { get; }

    Task SetDisplayNameAsync(string name);

    byte[] Sign(ReadOnlySpan<byte> data);

    bool Verify(byte[] publicKey, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature);
}
