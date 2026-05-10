namespace LocalWallet.Services.Sync;

/// <summary>
/// Last-write-wins comparison used by the projector. Higher Lamport clock wins;
/// ties are broken by ordinal compare of AuthorDeviceId so every replica picks
/// the same winner without coordination. Equal clock + equal author = same event,
/// treated as "not newer" so re-applying the same event is a no-op.
/// </summary>
public static class LamportOrder
{
    public static bool IsIncomingOlderOrEqual(
        long incomingClock, string incomingAuthor,
        long currentClock, string? currentAuthor)
    {
        if (incomingClock < currentClock) return true;
        if (incomingClock > currentClock) return false;
        return string.CompareOrdinal(incomingAuthor, currentAuthor ?? string.Empty) <= 0;
    }
}
