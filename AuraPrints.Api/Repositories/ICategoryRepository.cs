using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface ICategoryRepository
{
    List<Category> GetAll();
    Category Add(string name, string color);
    Category Update(int id, string name, string color);
    void Delete(int id);
}