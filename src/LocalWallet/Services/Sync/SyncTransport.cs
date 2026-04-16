using System.Buffers.Binary;
using System.Net.Sockets;

namespace LocalWallet.Services.Sync;

public static class SyncTransport
{
    public const int MaxFrame = 8 * 1024 * 1024; // 8 MB

    public static async Task WriteFrameAsync(NetworkStream stream, byte[] payload, CancellationToken ct = default)
    {
        if (payload.Length > MaxFrame) throw new InvalidOperationException("frame too large");
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await stream.WriteAsync(header, ct);
        if (payload.Length > 0) await stream.WriteAsync(payload, ct);
        await stream.FlushAsync(ct);
    }

    public static async Task<byte[]?> ReadFrameAsync(NetworkStream stream, CancellationToken ct = default)
    {
        var header = new byte[4];
        if (!await ReadExactAsync(stream, header, ct)) return null;
        var len = BinaryPrimitives.ReadInt32BigEndian(header);
        if (len < 0 || len > MaxFrame) return null;
        if (len == 0) return Array.Empty<byte>();
        var buf = new byte[len];
        if (!await ReadExactAsync(stream, buf, ct)) return null;
        return buf;
    }

    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct);
            if (n == 0) return false;
            read += n;
        }
        return true;
    }
}
