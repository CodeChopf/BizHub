using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IAttachmentRepository
{
    List<ExpenseAttachment> GetByExpenseId(int projectId, int expenseId);
    ExpenseAttachment Add(int projectId, int expenseId, string fileName, string mimeType, string data);
    void Delete(int projectId, int id);
}