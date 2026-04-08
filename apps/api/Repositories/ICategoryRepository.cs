using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface ICategoryRepository
{
    List<Category> GetAll(int projectId);
    Category Add(int projectId, string name, string color, string type = "expense");
    Category Update(int id, string name, string color);
    void Delete(int id);
}