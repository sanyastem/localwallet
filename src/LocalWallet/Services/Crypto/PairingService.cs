using System.Security.Cryptography;
using System.Text;
using NSec.Cryptography;

namespace LocalWallet.Services.Crypto;

public interface IPairingService
{
    EphemeralKeypair CreateEphemeralKeypair();

    PairingResult DeriveSession(
        EphemeralKeypair ours,
        byte[] peerPublicKey,
        byte[] saltConcat);
}

public readonly record struct EphemeralKeypair(byte[] PublicKey, byte[] PrivateKey)
{
    public void Clear()
    {
        if (PrivateKey is not null) CryptographicOperations.ZeroMemory(PrivateKey);
    }
}

public readonly record struct PairingResult(byte[] SessionKey, string Sas);

public class PairingService : IPairingService
{
    private const int SessionKeyLen = 32;
    private const int SasRawLen = 4;
    // Lazy so that a broken libsodium load doesn't blow up the type initializer
    // (which would kill DI resolution of every page that transitively depends on
    // this service).
    private static readonly Lazy<KeyAgreementAlgorithm?> KexLazy = new(() =>
    {
        try { return KeyAgreementAlgorithm.X25519; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Pairing] NSec unavailable: {ex}");
            return null;
        }
    });
    private static KeyAgreementAlgorithm Kex =>
        KexLazy.Value ?? throw new InvalidOperationException("Pairing: crypto unavailable on this device.");
    private static readonly byte[] Info = Encoding.UTF8.GetBytes("localwallet.pairing.v1");

    public EphemeralKeypair CreateEphemeralKeypair()
    {
        using var key = Key.Create(Kex, new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport
        });
        var priv = key.Export(KeyBlobFormat.RawPrivateKey);
        var pub = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        return new EphemeralKeypair(pub, priv);
    }

    public PairingResult DeriveSession(EphemeralKeypair ours, byte[] peerPublicKey, byte[] saltConcat)
    {
        using var priv = Key.Import(Kex, ours.PrivateKey, KeyBlobFormat.RawPrivateKey,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
        var peerPub = PublicKey.Import(Kex, peerPublicKey, KeyBlobFormat.RawPublicKey);

        var shared = Kex.Agree(priv, peerPub)
            ?? throw new CryptographicException("X25519 key agreement failed");

        var material = KeyDerivationAlgorithm.HkdfSha256.DeriveBytes(
            shared, saltConcat, Info, SessionKeyLen + SasRawLen);

        var sessionKey = material[..SessionKeyLen];
        var sasRaw = material.AsSpan(SessionKeyLen, SasRawLen);
        var sasInt = BitConverter.ToUInt32(sasRaw) % 1_000_000u;
        var sas = sasInt.ToString("D6");

        shared.Dispose();
        return new PairingResult(sessionKey, sas);
    }
}
