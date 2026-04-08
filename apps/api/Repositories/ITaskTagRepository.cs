using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface ITaskTagRepository
{
    List<TaskTag> GetAll(int projectId);
    TaskTag Add(int projectId, string name, string color);
    TaskTag Update(int id, string name, string color);
    void Delete(int id);
}
