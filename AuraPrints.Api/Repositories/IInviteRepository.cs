using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IInviteRepository
{
    Invite Create(string type, int? projectId, string role, int createdBy, int hoursValid = 48);
    Invite? GetByToken(string token);
    void MarkUsed(string token);
}
