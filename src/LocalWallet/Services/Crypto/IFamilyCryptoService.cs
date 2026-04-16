namespace LocalWallet.Services.Crypto;

public interface IFamilyCryptoService
{
    byte[] GenerateFamilyKey();

    AeadEnvelope Seal(byte[] key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData = default);

    byte[]? Open(byte[] key, AeadEnvelope envelope, ReadOnlySpan<byte> associatedData = default);
}

public readonly record struct AeadEnvelope(byte[] Nonce, byte[] CiphertextAndTag);
