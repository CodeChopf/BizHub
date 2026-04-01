using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IAttachmentRepository
{
    List<ExpenseAttachment> GetByExpenseId(int expenseId);
    ExpenseAttachment Add(int expenseId, string fileName, string mimeType, string data);
    void Delete(int id);
}