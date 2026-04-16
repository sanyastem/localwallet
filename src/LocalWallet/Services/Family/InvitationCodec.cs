using System.Text.Json;

namespace LocalWallet.Services.Families;

public record InvitationPayload(
    string V,
    string FamilyId,
    string FamilyName,
    string DeviceId,
    string EphemeralPub,
    string Ip,
    int Port,
    string SaltHex);

public static class InvitationCodec
{
    public const string Version = "1";

    public static string Encode(InvitationPayload p) => JsonSerializer.Serialize(p);

    public static InvitationPayload? Decode(string raw)
    {
        try
        {
            var p = JsonSerializer.Deserialize<InvitationPayload>(raw);
            if (p is null || p.V != Version) return null;
            return p;
        }
        catch
        {
            return null;
        }
    }
}
