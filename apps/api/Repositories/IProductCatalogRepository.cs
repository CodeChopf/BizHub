using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IProductCatalogRepository
{
    ProductCatalogData GetAll(int projectId);

    // Kategorien
    ProductCategory CreateCategory(int projectId, string name, string? description, string color);
    ProductCategory UpdateCategory(int projectId, int id, string name, string? description, string color);
    void DeleteCategory(int projectId, int id);

    // Attribute
    ProductAttribute AddAttribute(int projectId, int categoryId, string name, string fieldType, string? options, bool required, int sortOrder);
    void DeleteAttribute(int projectId, int id);

    // Produkte
    ProductV2 CreateProduct(int projectId, int categoryId, string name, string? description, string attributeValues);
    ProductV2 UpdateProduct(int projectId, int id, string name, string? description, string attributeValues);
    void DeleteProduct(int projectId, int id);

    // Variationen
    ProductVariation AddVariation(int projectId, int productId, string name, string sku, decimal price, int stock);
    ProductVariation UpdateVariation(int projectId, int id, string name, string sku, decimal price, int stock);
    void DeleteVariation(int projectId, int id);
    bool SkuExists(int projectId, string sku, int? excludeId = null);
    string GenerateSku(int projectId, int categoryId, int productId, string variationName);
}