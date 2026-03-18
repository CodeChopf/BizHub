using AuraPrintsApi.Data;
using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public class AttachmentRepository : IAttachmentRepository
{
    private readonly DatabaseContext _context;

    public AttachmentRepository(DatabaseContext context)
    {
        _context = context;
    }

    public List<ExpenseAttachment> GetByExpenseId(int expenseId)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            SELECT id, expense_id, file_name, mime_type, data, created_at
            FROM expense_attachments
            WHERE expense_id = @eid
            ORDER BY id";
        cmd.Parameters.AddWithValue("@eid", expenseId);
        var result = new List<ExpenseAttachment>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new ExpenseAttachment
            {
                Id = reader.GetInt32(0),
                ExpenseId = reader.GetInt32(1),
                FileName = reader.GetString(2),
                MimeType = reader.GetString(3),
                Data = reader.GetString(4),
                CreatedAt = reader.GetString(5)
            });
        }
        return result;
    }

    public ExpenseAttachment Add(int expenseId, string fileName, string mimeType, string data)
    {
        using var con = _context.CreateConnection();
        con.Open();
        var createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO expense_attachments (expense_id, file_name, mime_type, data, created_at)
            VALUES (@eid, @fn, @mt, @d, @ca);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@eid", expenseId);
        cmd.Parameters.AddWithValue("@fn", fileName);
        cmd.Parameters.AddWithValue("@mt", mimeType);
        cmd.Parameters.AddWithValue("@d", data);
        cmd.Parameters.AddWithValue("@ca", createdAt);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);

        return new ExpenseAttachment
        {
            Id = (int)id,
            ExpenseId = expenseId,
            FileName = fileName,
            MimeType = mimeType,
            Data = data,
            CreatedAt = createdAt
        };
    }

    public void Delete(int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM expense_attachments WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }
}