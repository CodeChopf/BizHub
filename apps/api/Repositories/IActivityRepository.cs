using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IActivityRepository
{
    List<ActivityEntry> GetRecent(int projectId, int limit = 20);
    void Add(int projectId, string entityType, string action, string title, string? description, string? actor);
}
