using System.Text.Json;
using LocalWallet.Models;
using LocalWallet.Services.Database;

namespace LocalWallet.Services.Sync;

public class Projector : IProjector
{
    private readonly IDatabaseService _db;

    public Projector(IDatabaseService db) => _db = db;

    public async Task ProjectAsync(SyncEvent ev, byte[] plaintextPayload)
    {
        switch (ev.EntityType)
        {
            case EntityType.Transaction:
                await ProjectTransactionAsync(ev, plaintextPayload);
                break;
            case EntityType.Account:
                await ProjectAccountAsync(ev, plaintextPayload);
                break;
            case EntityType.Category:
                await ProjectCategoryAsync(ev, plaintextPayload);
                break;
            case EntityType.Member:
                await ProjectMemberAsync(ev, plaintextPayload);
                break;
            case EntityType.ChatMessage:
                await ProjectChatMessageAsync(ev, plaintextPayload);
                break;
        }
    }

    private async Task ProjectChatMessageAsync(SyncEvent ev, byte[] payload)
    {
        if (ev.Operation == EventOperation.Delete)
        {
            await _db.DeleteChatMessageAsync(ev.EntityId);
            return;
        }
        var decoded = JsonSerializer.Deserialize<ChatMessage>(payload);
        if (decoded is null) return;
        var current = await _db.GetChatMessageAsync(ev.EntityId);
        if (current is not null && IsOlderOrEqual(ev, current.LamportClock, current.LastModifiedBy)) return;
        decoded.LamportClock = ev.LamportClock;
        decoded.LastModifiedBy = ev.AuthorDeviceId;
        decoded.AuthorDeviceId = ev.AuthorDeviceId;
        await _db.SaveChatMessageAsync(decoded);
    }

    private async Task ProjectTransactionAsync(SyncEvent ev, byte[] payload)
    {
        if (ev.Operation == EventOperation.Delete)
        {
            await _db.DeleteTransactionAsync(ev.EntityId);
            return;
        }
        var decoded = JsonSerializer.Deserialize<Transaction>(payload);
        if (decoded is null) return;
        var current = await _db.GetTransactionAsync(ev.EntityId);
        if (current is not null && IsOlderOrEqual(ev, current.LamportClock, current.LastModifiedBy)) return;
        decoded.LamportClock = ev.LamportClock;
        decoded.LastModifiedBy = ev.AuthorDeviceId;
        await _db.SaveTransactionAsync(decoded);
    }

    private async Task ProjectAccountAsync(SyncEvent ev, byte[] payload)
    {
        if (ev.Operation == EventOperation.Delete)
        {
            await _db.DeleteAccountAsync(ev.EntityId);
            return;
        }
        var decoded = JsonSerializer.Deserialize<Account>(payload);
        if (decoded is null) return;
        var current = await _db.GetAccountAsync(ev.EntityId);
        if (current is not null && IsOlderOrEqual(ev, current.LamportClock, current.LastModifiedBy)) return;
        decoded.LamportClock = ev.LamportClock;
        decoded.LastModifiedBy = ev.AuthorDeviceId;
        await _db.SaveAccountAsync(decoded);
    }

    private async Task ProjectCategoryAsync(SyncEvent ev, byte[] payload)
    {
        if (ev.Operation == EventOperation.Delete)
        {
            await _db.DeleteCategoryAsync(ev.EntityId);
            return;
        }
        var decoded = JsonSerializer.Deserialize<Category>(payload);
        if (decoded is null) return;
        var current = await _db.GetCategoryAsync(ev.EntityId);
        if (current is not null && IsOlderOrEqual(ev, current.LamportClock, current.LastModifiedBy)) return;
        decoded.LamportClock = ev.LamportClock;
        decoded.LastModifiedBy = ev.AuthorDeviceId;
        await _db.SaveCategoryAsync(decoded);
    }

    private async Task ProjectMemberAsync(SyncEvent ev, byte[] payload)
    {
        if (ev.Operation == EventOperation.Delete)
        {
            var p = JsonSerializer.Deserialize<MemberRevokePayload>(payload);
            if (string.IsNullOrWhiteSpace(p?.DeviceId)) return;
            var member = await _db.FindFamilyMemberAsync(ev.FamilyId, p.DeviceId);
            if (member is not null && member.RevokedAt is null)
            {
                member.RevokedAt = ev.CreatedAt;
                await _db.SaveFamilyMemberAsync(member);
            }
        }
    }

    private static bool IsOlderOrEqual(SyncEvent ev, long currentClock, string? currentAuthor)
    {
        if (ev.LamportClock < currentClock) return true;
        if (ev.LamportClock > currentClock) return false;
        return string.CompareOrdinal(ev.AuthorDeviceId, currentAuthor ?? string.Empty) <= 0;
    }
}

public record MemberRevokePayload(string DeviceId);
