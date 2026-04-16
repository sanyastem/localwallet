using LocalWallet.Models;

namespace LocalWallet.Services.Family;

public interface IFamilyService
{
    Task<List<Models.Family>> ListAsync();

    Task<Models.Family> CreateAsync(string name);

    Task<Models.Family> JoinAsync(Guid familyId, string name, byte[] familyKey, string inviterDeviceId, string inviterDisplayName);

    Task LeaveAsync(Guid familyId);

    Task RenameAsync(Guid familyId, string newName);

    Task<byte[]?> GetFamilyKeyAsync(Guid familyId);

    Task<List<FamilyMember>> GetMembersAsync(Guid familyId);

    Task UpsertMemberAsync(Guid familyId, string deviceId, string displayName, string role = "Member");
}
