using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IMilestoneRepository
{
    List<MilestoneListItem> GetAll(int projectId);
    Milestone GetById(int id);
    Milestone Create(int projectId, string name, string? description, string snapshot);
    void Delete(int id);
}