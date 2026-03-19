using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IGenericProductRepository
{
    ProductData2 GetAll();

    // Produkttypen
    ProductType CreateProductType(string name, string? description, string color);
    ProductType UpdateProductType(int id, string name, string? description, string color);
    void DeleteProductType(int id);

    // Felder
    ProductField AddField(int productTypeId, string name, string fieldType, string? options, bool required, int sortOrder);
    void UpdateField(int id, string name, string fieldType, string? options, bool required);
    void DeleteField(int id);

    // Produkte
    GenericProduct CreateProduct(int productTypeId, string fieldValues);
    GenericProduct UpdateProduct(int id, string fieldValues);
    void DeleteProduct(int id);
}