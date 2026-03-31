namespace AuraPrintsApi.Models;

public class FinanceData
{
    public List<Category> Categories { get; set; } = new();
    public List<Expense> Expenses { get; set; } = new();
    public decimal TotalExpenses { get; set; }
    public List<CategorySummary> Summary { get; set; } = new();
}

public class CategorySummary
{
    public string CategoryName { get; set; } = "";
    public string CategoryColor { get; set; } = "";
    public decimal Total { get; set; }
    public int Count { get; set; }
}