using LocalWallet.Services.Sync;

namespace LocalWallet.Tests;

public class LamportOrderTests
{
    [Fact]
    public void Lower_clock_is_older()
    {
        Assert.True(LamportOrder.IsIncomingOlderOrEqual(
            incomingClock: 5, incomingAuthor: "zzz",
            currentClock: 6, currentAuthor: "aaa"));
    }

    [Fact]
    public void Higher_clock_wins_regardless_of_author()
    {
        Assert.False(LamportOrder.IsIncomingOlderOrEqual(
            incomingClock: 7, incomingAuthor: "aaa",
            currentClock: 6, currentAuthor: "zzz"));
    }

    [Fact]
    public void Equal_clock_breaks_tie_by_ordinal_author()
    {
        // "aaa" < "zzz" → "aaa" loses (is older-or-equal)
        Assert.True(LamportOrder.IsIncomingOlderOrEqual(
            incomingClock: 5, incomingAuthor: "aaa",
            currentClock: 5, currentAuthor: "zzz"));

        // "zzz" > "aaa" → "zzz" wins (is NOT older-or-equal)
        Assert.False(LamportOrder.IsIncomingOlderOrEqual(
            incomingClock: 5, incomingAuthor: "zzz",
            currentClock: 5, currentAuthor: "aaa"));
    }

    [Fact]
    public void Same_clock_same_author_is_treated_as_equal()
    {
        // Re-applying the same event must be a no-op, so equal IDs map to "older or equal".
        Assert.True(LamportOrder.IsIncomingOlderOrEqual(
            incomingClock: 42, incomingAuthor: "device-1",
            currentClock: 42, currentAuthor: "device-1"));
    }

    [Fact]
    public void Null_current_author_is_treated_as_empty_string()
    {
        // currentAuthor=null happens for fresh rows that haven't been touched yet.
        // Any non-empty incoming author beats it on the tie-break.
        Assert.False(LamportOrder.IsIncomingOlderOrEqual(
            incomingClock: 0, incomingAuthor: "device-1",
            currentClock: 0, currentAuthor: null));

        // Empty incoming with null current → equal → "older or equal"
        Assert.True(LamportOrder.IsIncomingOlderOrEqual(
            incomingClock: 0, incomingAuthor: "",
            currentClock: 0, currentAuthor: null));
    }

    [Fact]
    public void Comparison_uses_ordinal_not_culture()
    {
        // In some cultures uppercase sorts after lowercase, but ordinal compare
        // puts uppercase ASCII first. Conflict resolution must be culture-agnostic
        // so every replica picks the same winner.
        Assert.True(LamportOrder.IsIncomingOlderOrEqual(
            incomingClock: 1, incomingAuthor: "A",
            currentClock: 1, currentAuthor: "a"));
    }
}
