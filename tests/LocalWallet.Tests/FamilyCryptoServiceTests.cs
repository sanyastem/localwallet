using System.Text;
using LocalWallet.Services.Crypto;

namespace LocalWallet.Tests;

public class FamilyCryptoServiceTests
{
    private readonly FamilyCryptoService _sut = new();

    [Fact]
    public void GenerateFamilyKey_returns_32_random_bytes()
    {
        var k1 = _sut.GenerateFamilyKey();
        var k2 = _sut.GenerateFamilyKey();

        Assert.Equal(32, k1.Length);
        Assert.Equal(32, k2.Length);
        Assert.NotEqual(k1, k2);
    }

    [Fact]
    public void Seal_then_Open_with_same_key_and_AD_returns_plaintext()
    {
        var key = _sut.GenerateFamilyKey();
        var plaintext = Encoding.UTF8.GetBytes("hello family");
        var ad = Encoding.UTF8.GetBytes("family-id|0|entity-id|0|1");

        var env = _sut.Seal(key, plaintext, ad);
        var decoded = _sut.Open(key, env, ad);

        Assert.NotNull(decoded);
        Assert.Equal(plaintext, decoded);
    }

    [Fact]
    public void Seal_produces_fresh_nonce_each_call()
    {
        var key = _sut.GenerateFamilyKey();
        var plaintext = Encoding.UTF8.GetBytes("same payload");

        var a = _sut.Seal(key, plaintext);
        var b = _sut.Seal(key, plaintext);

        Assert.NotEqual(a.Nonce, b.Nonce);
        Assert.NotEqual(a.CiphertextAndTag, b.CiphertextAndTag);
    }

    [Fact]
    public void Open_with_wrong_key_returns_null()
    {
        var key = _sut.GenerateFamilyKey();
        var other = _sut.GenerateFamilyKey();
        var env = _sut.Seal(key, Encoding.UTF8.GetBytes("secret"));

        Assert.Null(_sut.Open(other, env));
    }

    [Fact]
    public void Open_with_wrong_associated_data_returns_null()
    {
        var key = _sut.GenerateFamilyKey();
        var ad1 = Encoding.UTF8.GetBytes("ad-1");
        var ad2 = Encoding.UTF8.GetBytes("ad-2");
        var env = _sut.Seal(key, Encoding.UTF8.GetBytes("payload"), ad1);

        Assert.Null(_sut.Open(key, env, ad2));
    }

    [Fact]
    public void Open_with_tampered_ciphertext_returns_null()
    {
        var key = _sut.GenerateFamilyKey();
        var env = _sut.Seal(key, Encoding.UTF8.GetBytes("immutable"));

        env.CiphertextAndTag[0] ^= 0x01;

        Assert.Null(_sut.Open(key, env));
    }

    [Fact]
    public void Open_with_tampered_nonce_returns_null()
    {
        var key = _sut.GenerateFamilyKey();
        var env = _sut.Seal(key, Encoding.UTF8.GetBytes("immutable"));

        env.Nonce[0] ^= 0x01;

        Assert.Null(_sut.Open(key, env));
    }

    [Fact]
    public void Open_with_malformed_envelope_returns_null()
    {
        var key = _sut.GenerateFamilyKey();

        Assert.Null(_sut.Open(key, new AeadEnvelope(new byte[5], new byte[20])));
        Assert.Null(_sut.Open(key, new AeadEnvelope(new byte[12], new byte[8])));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    public void Seal_with_wrong_key_size_throws(int badLength)
    {
        var key = new byte[badLength];

        Assert.Throws<ArgumentException>(() => _sut.Seal(key, Encoding.UTF8.GetBytes("x")));
    }

    [Fact]
    public void Empty_plaintext_roundtrips()
    {
        var key = _sut.GenerateFamilyKey();
        var env = _sut.Seal(key, ReadOnlySpan<byte>.Empty);
        var decoded = _sut.Open(key, env);

        Assert.NotNull(decoded);
        Assert.Empty(decoded!);
    }
}
