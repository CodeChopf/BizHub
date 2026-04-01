namespace AuraPrintsApi.Models;

public class CostItem
{
    public string Label { get; set; } = "";
    public string Amount { get; set; } = "";
}

public class CostCalc
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string SalePrice { get; set; } = "";
    public List<CostItem> Costs { get; set; } = new();
    public string Profit { get; set; } = "";
}