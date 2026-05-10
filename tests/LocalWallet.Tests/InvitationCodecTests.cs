using LocalWallet.Services.Families;

namespace LocalWallet.Tests;

public class InvitationCodecTests
{
    [Fact]
    public void Encode_then_Decode_roundtrips()
    {
        var p = new InvitationPayload(
            V: InvitationCodec.Version,
            FamilyId: Guid.NewGuid().ToString("N"),
            FamilyName: "Sample family",
            DeviceId: "device-1",
            EphemeralPub: "AAAA",
            Ip: "192.168.1.10",
            Port: 47321,
            SaltHex: "deadbeef");

        var encoded = InvitationCodec.Encode(p);
        var decoded = InvitationCodec.Decode(encoded);

        Assert.NotNull(decoded);
        Assert.Equal(p, decoded);
    }

    [Fact]
    public void Decode_returns_null_for_wrong_version()
    {
        var p = new InvitationPayload(
            V: "999",
            FamilyId: Guid.NewGuid().ToString("N"),
            FamilyName: "x",
            DeviceId: "d",
            EphemeralPub: "AAAA",
            Ip: "1.2.3.4",
            Port: 1,
            SaltHex: "00");

        Assert.Null(InvitationCodec.Decode(InvitationCodec.Encode(p)));
    }

    [Fact]
    public void Decode_returns_null_for_garbage()
    {
        Assert.Null(InvitationCodec.Decode("not-json"));
        Assert.Null(InvitationCodec.Decode("{}"));
        Assert.Null(InvitationCodec.Decode(string.Empty));
    }
}
