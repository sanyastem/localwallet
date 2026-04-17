using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

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
    private static readonly byte[] Info = Encoding.UTF8.GetBytes("localwallet.pairing.v1");

    public EphemeralKeypair CreateEphemeralKeypair()
    {
        var seed = new byte[X25519PrivateKeyParameters.KeySize];
        RandomNumberGenerator.Fill(seed);
        var priv = new X25519PrivateKeyParameters(seed, 0);
        var pub = priv.GeneratePublicKey().GetEncoded();
        return new EphemeralKeypair(pub, seed);
    }

    public PairingResult DeriveSession(EphemeralKeypair ours, byte[] peerPublicKey, byte[] saltConcat)
    {
        var priv = new X25519PrivateKeyParameters(ours.PrivateKey, 0);
        var peerPub = new X25519PublicKeyParameters(peerPublicKey, 0);

        var agreement = new X25519Agreement();
        agreement.Init(priv);
        var shared = new byte[agreement.AgreementSize];
        agreement.CalculateAgreement(peerPub, shared, 0);

        var hkdf = new HkdfBytesGenerator(new Sha256Digest());
        hkdf.Init(new HkdfParameters(shared, saltConcat, Info));
        var material = new byte[SessionKeyLen + SasRawLen];
        hkdf.GenerateBytes(material, 0, material.Length);

        var sessionKey = new byte[SessionKeyLen];
        Buffer.BlockCopy(material, 0, sessionKey, 0, SessionKeyLen);

        var sasInt = BitConverter.ToUInt32(material, SessionKeyLen) % 1_000_000u;
        var sas = sasInt.ToString("D6");

        CryptographicOperations.ZeroMemory(shared);
        CryptographicOperations.ZeroMemory(material);
        return new PairingResult(sessionKey, sas);
    }
}
