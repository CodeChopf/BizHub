namespace AuraPrintsApi.Models;

public class ProductField
{
    public int Id { get; set; }
    public int ProductTypeId { get; set; }
    public string Name { get; set; } = "";
    public string FieldType { get; set; } = "text"; // text, number, url, select, textarea
    public string? Options { get; set; } // Komma-getrennte Optionen für select
    public bool Required { get; set; } = false;
    public int SortOrder { get; set; }
}

public class ProductType
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Color { get; set; } = "#4f8ef7";
    public List<ProductField> Fields { get; set; } = new();
}

public class GenericProduct
{
    public int Id { get; set; }
    public int ProductTypeId { get; set; }
    public string ProductTypeName { get; set; } = "";
    public string ProductTypeColor { get; set; } = "";
    public string FieldValues { get; set; } = "{}"; // JSON
    public string CreatedAt { get; set; } = "";
}

public class ProductData2
{
    public List<ProductType> ProductTypes { get; set; } = new();
    public List<GenericProduct> Products { get; set; } = new();
}