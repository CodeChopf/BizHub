using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IProductCatalogRepository
{
    ProductCatalogData GetAll();

    // Kategorien
    ProductCategory CreateCategory(string name, string? description, string color);
    ProductCategory UpdateCategory(int id, string name, string? description, string color);
    void DeleteCategory(int id);

    // Attribute
    ProductAttribute AddAttribute(int categoryId, string name, string fieldType, string? options, bool required, int sortOrder);
    void DeleteAttribute(int id);

    // Produkte
    ProductV2 CreateProduct(int categoryId, string name, string? description, string attributeValues);
    ProductV2 UpdateProduct(int id, string name, string? description, string attributeValues);
    void DeleteProduct(int id);

    // Variationen
    ProductVariation AddVariation(int productId, string name, string sku, decimal price, int stock);
    ProductVariation UpdateVariation(int id, string name, string sku, decimal price, int stock);
    void DeleteVariation(int id);
    bool SkuExists(string sku, int? excludeId = null);
    string GenerateSku(int categoryId, int productId, string variationName);
}