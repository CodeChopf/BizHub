using System.Text.Json;

namespace AuraPrintsApi.Models;

public class ProductCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Color { get; set; } = "#4f8ef7";
    public List<ProductAttribute> Attributes { get; set; } = new();
}

public class ProductAttribute
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = "";
    public string FieldType { get; set; } = "text"; // text, number, url, select, textarea
    public string? Options { get; set; }
    public bool Required { get; set; } = false;
    public int SortOrder { get; set; }
}

public class ProductV2
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public string CategoryColor { get; set; } = "#4f8ef7";
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    // Intern als String gespeichert
    [System.Text.Json.Serialization.JsonIgnore]
    public string AttributeValues { get; set; } = "{}";

    // Nach aussen als echtes JSON-Objekt serialisieren
    [System.Text.Json.Serialization.JsonPropertyName("attributeValues")]
    public JsonElement AttributeValuesJson
    {
        get
        {
            try
            {
                if (string.IsNullOrWhiteSpace(AttributeValues))
                    return JsonSerializer.Deserialize<JsonElement>("{}");
                return JsonSerializer.Deserialize<JsonElement>(AttributeValues);
            }
            catch
            {
                return JsonSerializer.Deserialize<JsonElement>("{}");
            }
        }
    }

    public string CreatedAt { get; set; } = "";
    public List<ProductVariation> Variations { get; set; } = new();
}

public class ProductVariation
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string CreatedAt { get; set; } = "";
}

public class ProductCatalogData
{
    public List<ProductCategory> Categories { get; set; } = new();
    public List<ProductV2> Products { get; set; } = new();
}