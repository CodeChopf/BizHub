using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IMilestoneRepository
{
    List<MilestoneListItem> GetAll();
    Milestone GetById(int id);
    Milestone Create(string name, string? description, string snapshot);
    void Delete(int id);
}