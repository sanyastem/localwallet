using System.Security.Cryptography;

namespace LocalWallet.Services.Crypto;

public class FamilyCryptoService : IFamilyCryptoService
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public byte[] GenerateFamilyKey()
    {
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    public AeadEnvelope Seal(byte[] key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData = default)
    {
        ValidateKey(key);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aead = new ChaCha20Poly1305(key);
        aead.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

        var combined = new byte[ciphertext.Length + TagSize];
        Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, TagSize);

        return new AeadEnvelope(nonce, combined);
    }

    public byte[]? Open(byte[] key, AeadEnvelope envelope, ReadOnlySpan<byte> associatedData = default)
    {
        ValidateKey(key);
        if (envelope.Nonce is null || envelope.Nonce.Length != NonceSize) return null;
        if (envelope.CiphertextAndTag is null || envelope.CiphertextAndTag.Length < TagSize) return null;

        var ciphertextLen = envelope.CiphertextAndTag.Length - TagSize;
        var ciphertext = envelope.CiphertextAndTag.AsSpan(0, ciphertextLen);
        var tag = envelope.CiphertextAndTag.AsSpan(ciphertextLen, TagSize);
        var plaintext = new byte[ciphertextLen];

        try
        {
            using var aead = new ChaCha20Poly1305(key);
            aead.Decrypt(envelope.Nonce, ciphertext, tag, plaintext, associatedData);
            return plaintext;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static void ValidateKey(byte[] key)
    {
        if (key is null || key.Length != KeySize)
            throw new ArgumentException($"FamilyKey must be {KeySize} bytes", nameof(key));
    }
}
