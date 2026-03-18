using AuraPrintsApi.Data;
using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public class ExpenseRepository : IExpenseRepository
{
    private readonly DatabaseContext _context;

    public ExpenseRepository(DatabaseContext context)
    {
        _context = context;
    }

    public FinanceData GetAll()
    {
        using var con = _context.CreateConnection();
        con.Open();

        // Kategorien laden
        var categories = new List<Category>();
        using var cCmd = con.CreateCommand();
        cCmd.CommandText = "SELECT id, name, color FROM categories ORDER BY name";
        using var cReader = cCmd.ExecuteReader();
        while (cReader.Read())
        {
            categories.Add(new Category
            {
                Id = cReader.GetInt32(0),
                Name = cReader.GetString(1),
                Color = cReader.GetString(2)
            });
        }

        // Ausgaben laden
        var expenses = new List<Expense>();
        using var eCmd = con.CreateCommand();
        eCmd.CommandText = @"
            SELECT e.id, e.category_id, c.name, c.color, e.amount,
                   e.description, e.link, e.date, e.week_number, e.task_id
            FROM expenses e
            JOIN categories c ON c.id = e.category_id
            ORDER BY e.date DESC";
        using var eReader = eCmd.ExecuteReader();
        while (eReader.Read())
        {
            expenses.Add(new Expense
            {
                Id = eReader.GetInt32(0),
                CategoryId = eReader.GetInt32(1),
                CategoryName = eReader.GetString(2),
                CategoryColor = eReader.GetString(3),
                Amount = eReader.GetDecimal(4),
                Description = eReader.GetString(5),
                Link = eReader.IsDBNull(6) ? null : eReader.GetString(6),
                Date = eReader.GetString(7),
                WeekNumber = eReader.IsDBNull(8) ? null : eReader.GetInt32(8),
                TaskId = eReader.IsDBNull(9) ? null : eReader.GetInt32(9)
            });
        }

        // Summary berechnen
        var summary = expenses
            .GroupBy(e => new { e.CategoryName, e.CategoryColor })
            .Select(g => new CategorySummary
            {
                CategoryName = g.Key.CategoryName,
                CategoryColor = g.Key.CategoryColor,
                Total = g.Sum(e => e.Amount),
                Count = g.Count()
            })
            .OrderByDescending(s => s.Total)
            .ToList();

        return new FinanceData
        {
            Categories = categories,
            Expenses = expenses,
            TotalExpenses = expenses.Sum(e => e.Amount),
            Summary = summary
        };
    }

    public Expense Add(int categoryId, decimal amount, string description, string? link, string date, int? weekNumber, int? taskId)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO expenses (category_id, amount, description, link, date, week_number, task_id)
            VALUES (@c, @a, @d, @l, @dt, @w, @t);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@c", categoryId);
        cmd.Parameters.AddWithValue("@a", amount);
        cmd.Parameters.AddWithValue("@d", description);
        cmd.Parameters.AddWithValue("@l", (object?)link ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@dt", date);
        cmd.Parameters.AddWithValue("@w", (object?)weekNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@t", (object?)taskId ?? DBNull.Value);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);

        // Vollständige Expense mit Kategorie zurückgeben
        using var rCmd = con.CreateCommand();
        rCmd.CommandText = @"
            SELECT e.id, e.category_id, c.name, c.color, e.amount,
                   e.description, e.link, e.date, e.week_number, e.task_id
            FROM expenses e
            JOIN categories c ON c.id = e.category_id
            WHERE e.id = @id";
        rCmd.Parameters.AddWithValue("@id", id);
        using var r = rCmd.ExecuteReader();
        r.Read();
        return new Expense
        {
            Id = r.GetInt32(0),
            CategoryId = r.GetInt32(1),
            CategoryName = r.GetString(2),
            CategoryColor = r.GetString(3),
            Amount = r.GetDecimal(4),
            Description = r.GetString(5),
            Link = r.IsDBNull(6) ? null : r.GetString(6),
            Date = r.GetString(7),
            WeekNumber = r.IsDBNull(8) ? null : r.GetInt32(8),
            TaskId = r.IsDBNull(9) ? null : r.GetInt32(9)
        };
    }

    public void Delete(int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM expenses WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }
}