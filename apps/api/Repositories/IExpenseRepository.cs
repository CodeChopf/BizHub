using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IExpenseRepository
{
    FinanceData GetAll(int projectId);
    Expense Add(int projectId, int categoryId, decimal amount, string description, string? link, string date, int? weekNumber, int? taskId, string type = "expense");
    void Delete(int id);
    Expense Update(int id, int categoryId, decimal amount, string description, string? link, string date, int? weekNumber, int? taskId, string type = "expense");
}