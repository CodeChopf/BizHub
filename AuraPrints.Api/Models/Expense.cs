namespace AuraPrintsApi.Models;

public class Expense
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public string CategoryColor { get; set; } = "";
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public string? Link { get; set; }
    public string Date { get; set; } = "";
    public int? WeekNumber { get; set; }
    public int? TaskId { get; set; }
}