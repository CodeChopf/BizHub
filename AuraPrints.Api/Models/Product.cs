namespace AuraPrintsApi.Models;

public class ProductVariant
{
    public string Size { get; set; } = "";
    public string Height { get; set; } = "";
    public string PrintTime { get; set; } = "";
    public string Price { get; set; } = "";
}

public class Product
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public List<ProductVariant> Variants { get; set; } = new();
}