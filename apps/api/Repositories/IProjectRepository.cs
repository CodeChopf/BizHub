using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IProjectRepository
{
    List<Project> GetForUser(int userId);
    Project? GetById(int id);
    Project Create(string name, string? description, string? startDate, string currency, int adminUserId);
    void Update(int id, string name, string? description, string? startDate, string currency, string? projectImage, string? visibleTabs);
    List<ProjectMember> GetMembers(int projectId);
    void AddMember(int projectId, int userId, string role);
    void RemoveMember(int projectId, int userId);
    bool IsMember(int projectId, int userId);
    string? GetRole(int projectId, int userId);
    void DeleteProject(int id);
}
