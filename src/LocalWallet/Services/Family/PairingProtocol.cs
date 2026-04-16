using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LocalWallet.Services.Crypto;
using LocalWallet.Services.Sync;

namespace LocalWallet.Services.Family;

public class PairingHostMessages
{
    public class Hello
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string EphemeralPubBase64 { get; set; } = string.Empty;
        public string SaltBase64 { get; set; } = string.Empty;
    }

    public class Sealed
    {
        public string NonceBase64 { get; set; } = string.Empty;
        public string CiphertextBase64 { get; set; } = string.Empty;
    }

    public class FamilyBundle
    {
        public string FamilyId { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public string FamilyKeyBase64 { get; set; } = string.Empty;
        public string OwnerDeviceId { get; set; } = string.Empty;
        public string OwnerDisplayName { get; set; } = string.Empty;
    }
}

public class PairingHostSession
{
    public required EphemeralKeypair Ours { get; init; }
    public required byte[] OurSalt { get; init; }
    public required TcpListener Listener { get; init; }
    public required int Port { get; init; }
    public required string LocalIp { get; init; }

    public string? PeerDeviceId { get; set; }
    public string? PeerDisplayName { get; set; }
    public byte[]? SessionKey { get; set; }
    public string? Sas { get; set; }
    public TcpClient? Client { get; set; }
    public NetworkStream? Stream { get; set; }
}

public class PairingClientSession
{
    public required EphemeralKeypair Ours { get; init; }
    public required byte[] OurSalt { get; init; }
    public TcpClient? Client { get; set; }
    public NetworkStream? Stream { get; set; }
    public byte[]? SessionKey { get; set; }
    public string? Sas { get; set; }
}

public static class LocalNetwork
{
    public static string? GetLocalIPv4()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(ua.Address))
                        return ua.Address.ToString();
                }
            }
        }
        catch { }
        return null;
    }
}
