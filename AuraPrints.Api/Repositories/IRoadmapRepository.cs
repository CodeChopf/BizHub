using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IRoadmapRepository
{
    AppData GetAll(int projectId);
}