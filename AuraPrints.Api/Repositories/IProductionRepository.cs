using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IProductionRepository
{
    List<ProductionItem> GetAll(int projectId);
    ProductionItem Add(int projectId, int productId, int? variationId, int quantity, string? note);
    void SetDone(int id, bool done);
    void UpdateItem(int id, int quantity, string? note);
    void Delete(int id);
    void DeleteAllDone();
}
