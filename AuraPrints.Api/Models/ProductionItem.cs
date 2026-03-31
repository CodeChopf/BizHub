namespace AuraPrintsApi.Models;

public class ProductionItem
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string CategoryColor { get; set; } = "";
    public int? VariationId { get; set; }
    public string? VariationName { get; set; }
    public string? VariationSku { get; set; }
    public int Quantity { get; set; }
    public bool Done { get; set; }
    public string? Note { get; set; }
    public string AddedAt { get; set; } = "";
}
