namespace AuraPrintsApi.Models;

public class ProductData
{
    public List<Product> Products { get; set; } = new();
    public List<CostCalc> Calculations { get; set; } = new();
    public List<Phase2Item> Phase2 { get; set; } = new();
    public List<LegalItem> Legal { get; set; } = new();
}