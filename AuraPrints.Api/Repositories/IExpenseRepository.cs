using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IExpenseRepository
{
    FinanceData GetAll();
    Expense Add(int categoryId, decimal amount, string description, string? link, string date, int? weekNumber, int? taskId);
    void Delete(int id);
    Expense Update(int id, int categoryId, decimal amount, string description, string? link, string date, int? weekNumber, int? taskId);
}