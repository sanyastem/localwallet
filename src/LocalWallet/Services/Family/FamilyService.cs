using LocalWallet.Services.Crypto;
using LocalWallet.Services.Database;

namespace LocalWallet.Services.Families;

public class FamilyService : IFamilyService
{
    private const string FamilyKeyPrefix = "lw.family.key.";

    private readonly IDatabaseService _db;
    private readonly IDeviceIdentityService _identity;
    private readonly IFamilyCryptoService _crypto;

    public FamilyService(IDatabaseService db, IDeviceIdentityService identity, IFamilyCryptoService crypto)
    {
        _db = db;
        _identity = identity;
        _crypto = crypto;
    }

    public Task<List<Models.Family>> ListAsync() => _db.GetFamiliesAsync();

    public async Task<Models.Family> CreateAsync(string name)
    {
        await _identity.InitializeAsync();

        var family = new Models.Family
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(name) ? "Семья" : name.Trim(),
            Role = "Owner",
            CreatedAt = DateTime.UtcNow
        };
        await _db.SaveFamilyAsync(family);

        var familyKey = _crypto.GenerateFamilyKey();
        await StoreFamilyKeyAsync(family.Id, familyKey);

        await UpsertMemberAsync(family.Id, _identity.DeviceId, _identity.DisplayName, "Owner");
        return family;
    }

    public async Task<Models.Family> JoinAsync(Guid familyId, string name, byte[] familyKey, string inviterDeviceId, string inviterDisplayName)
    {
        await _identity.InitializeAsync();

        var existing = await _db.GetFamilyAsync(familyId);
        var family = existing ?? new Models.Family
        {
            Id = familyId,
            Name = name,
            Role = "Member",
            CreatedAt = DateTime.UtcNow
        };
        family.Name = string.IsNullOrWhiteSpace(name) ? family.Name : name;
        if (string.IsNullOrWhiteSpace(family.Role)) family.Role = "Member";
        family.IsDeleted = false;
        await _db.SaveFamilyAsync(family);

        await StoreFamilyKeyAsync(family.Id, familyKey);

        await UpsertMemberAsync(family.Id, _identity.DeviceId, _identity.DisplayName, "Member");
        if (!string.IsNullOrWhiteSpace(inviterDeviceId))
        {
            await UpsertMemberAsync(family.Id, inviterDeviceId, inviterDisplayName, "Owner");
        }
        return family;
    }

    public async Task LeaveAsync(Guid familyId)
    {
        await _db.DeleteFamilyAsync(familyId);
        try { SecureStorage.Default.Remove(FamilyKeyPrefix + familyId.ToString("N")); } catch { }
    }

    public async Task RenameAsync(Guid familyId, string newName)
    {
        var family = await _db.GetFamilyAsync(familyId);
        if (family is null) return;
        family.Name = newName.Trim();
        await _db.SaveFamilyAsync(family);
    }

    public async Task<byte[]?> GetFamilyKeyAsync(Guid familyId)
    {
        try
        {
            var raw = await SecureStorage.Default.GetAsync(FamilyKeyPrefix + familyId.ToString("N"));
            return string.IsNullOrEmpty(raw) ? null : Convert.FromBase64String(raw);
        }
        catch
        {
            return null;
        }
    }

    public Task<List<Models.FamilyMember>> GetMembersAsync(Guid familyId) => _db.GetFamilyMembersAsync(familyId);

    public async Task UpsertMemberAsync(Guid familyId, string deviceId, string displayName, string role = "Member")
    {
        var existing = await _db.FindFamilyMemberAsync(familyId, deviceId);
        if (existing is null)
        {
            await _db.SaveFamilyMemberAsync(new Models.FamilyMember
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId,
                DeviceId = deviceId,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? deviceId[..Math.Min(8, deviceId.Length)] : displayName,
                Role = role,
                JoinedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.DisplayName = string.IsNullOrWhiteSpace(displayName) ? existing.DisplayName : displayName;
            if (role == "Owner") existing.Role = "Owner";
            await _db.SaveFamilyMemberAsync(existing);
        }
    }

    private static async Task StoreFamilyKeyAsync(Guid familyId, byte[] key)
    {
        try
        {
            await SecureStorage.Default.SetAsync(FamilyKeyPrefix + familyId.ToString("N"), Convert.ToBase64String(key));
        }
        catch
        {
            // SecureStorage unavailable — family key is still kept in-memory for current session
        }
    }
}
