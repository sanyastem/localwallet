using System.Text.Json;
using LocalWallet.Models;
using LocalWallet.Services.Database;
using LocalWallet.Services.Families;
using LocalWallet.Services.Sync;

namespace LocalWallet.Services;

public interface IEntityWriter
{
    Task SaveTransactionAsync(Transaction t);
    Task SaveAccountAsync(Account a);
    Task SaveCategoryAsync(Category c);
    Task DeleteTransactionAsync(Transaction t);
    Task DeleteAccountAsync(Account a);
    Task DeleteCategoryAsync(Category c);
}

public class EntityWriter : IEntityWriter
{
    private readonly IDatabaseService _db;
    private readonly IEventStore _events;
    private readonly IFamilyService _families;

    public EntityWriter(IDatabaseService db, IEventStore events, IFamilyService families)
    {
        _db = db;
        _events = events;
        _families = families;
    }

    public Task SaveTransactionAsync(Transaction t) =>
        t.FamilyId is Guid fid
            ? EmitUpsertAsync(fid, EntityType.Transaction, t.Id, t, fallback: () => _db.SaveTransactionAsync(t))
            : _db.SaveTransactionAsync(t);

    public Task SaveAccountAsync(Account a) =>
        a.FamilyId is Guid fid
            ? EmitUpsertAsync(fid, EntityType.Account, a.Id, a, fallback: () => _db.SaveAccountAsync(a))
            : _db.SaveAccountAsync(a);

    public Task SaveCategoryAsync(Category c) =>
        c.FamilyId is Guid fid
            ? EmitUpsertAsync(fid, EntityType.Category, c.Id, c, fallback: () => _db.SaveCategoryAsync(c))
            : _db.SaveCategoryAsync(c);

    public Task DeleteTransactionAsync(Transaction t) =>
        t.FamilyId is Guid fid
            ? EmitDeleteAsync(fid, EntityType.Transaction, t.Id, fallback: () => _db.DeleteTransactionAsync(t.Id))
            : _db.DeleteTransactionAsync(t.Id);

    public Task DeleteAccountAsync(Account a) =>
        a.FamilyId is Guid fid
            ? EmitDeleteAsync(fid, EntityType.Account, a.Id, fallback: () => _db.DeleteAccountAsync(a.Id))
            : _db.DeleteAccountAsync(a.Id);

    public Task DeleteCategoryAsync(Category c) =>
        c.FamilyId is Guid fid
            ? EmitDeleteAsync(fid, EntityType.Category, c.Id, fallback: () => _db.DeleteCategoryAsync(c.Id))
            : _db.DeleteCategoryAsync(c.Id);

    private async Task EmitUpsertAsync<T>(Guid familyId, EntityType type, Guid entityId, T entity, Func<Task> fallback)
    {
        var key = await _families.GetFamilyKeyAsync(familyId);
        if (key is null) { await fallback(); return; }
        var payload = JsonSerializer.SerializeToUtf8Bytes(entity);
        await _events.AppendLocalAsync(
            new EventDraft(familyId, type, entityId, EventOperation.Upsert, payload),
            key);
    }

    private async Task EmitDeleteAsync(Guid familyId, EntityType type, Guid entityId, Func<Task> fallback)
    {
        var key = await _families.GetFamilyKeyAsync(familyId);
        if (key is null) { await fallback(); return; }
        await _events.AppendLocalAsync(
            new EventDraft(familyId, type, entityId, EventOperation.Delete, Array.Empty<byte>()),
            key);
    }
}
