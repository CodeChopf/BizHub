using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IProductionRepository
{
    List<ProductionItem> GetAll(int projectId);
    ProductionItem Add(int projectId, int productId, int? variationId, int quantity, string? note);
    void SetDone(int projectId, int id, bool done);
    void UpdateItem(int projectId, int id, int quantity, string? note);
    void Delete(int projectId, int id);
    void DeleteAllDone(int projectId);
}
