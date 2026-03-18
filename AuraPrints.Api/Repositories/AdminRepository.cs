using AuraPrintsApi.Data;
using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public class AdminRepository : IAdminRepository
{
    private readonly DatabaseContext _context;

    public AdminRepository(DatabaseContext context)
    {
        _context = context;
    }

    // ── WOCHEN ──

    public Week CreateWeek(CreateWeekRequest req)
    {
        using var con = _context.CreateConnection();
        con.Open();

        // Nächste Wochennummer ermitteln
        using var numCmd = con.CreateCommand();
        numCmd.CommandText = "SELECT COALESCE(MAX(number), 0) + 1 FROM weeks";
        var nextNumber = (long)(numCmd.ExecuteScalar() ?? 1L);

        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO weeks (number, title, phase, badge_pc, badge_phys, note)
            VALUES (@n, @t, @p, @bp, @bph, @no)";
        cmd.Parameters.AddWithValue("@n", nextNumber);
        cmd.Parameters.AddWithValue("@t", req.Title);
        cmd.Parameters.AddWithValue("@p", req.Phase);
        cmd.Parameters.AddWithValue("@bp", req.BadgePc);
        cmd.Parameters.AddWithValue("@bph", req.BadgePhys);
        cmd.Parameters.AddWithValue("@no", (object?)req.Note ?? DBNull.Value);
        cmd.ExecuteNonQuery();

        return new Week
        {
            Number = (int)nextNumber,
            Title = req.Title,
            Phase = req.Phase,
            BadgePc = req.BadgePc,
            BadgePhys = req.BadgePhys,
            Note = req.Note
        };
    }

    public Week UpdateWeek(int number, UpdateWeekRequest req)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            UPDATE weeks SET title = @t, phase = @p, badge_pc = @bp, badge_phys = @bph, note = @no
            WHERE number = @n";
        cmd.Parameters.AddWithValue("@t", req.Title);
        cmd.Parameters.AddWithValue("@p", req.Phase);
        cmd.Parameters.AddWithValue("@bp", req.BadgePc);
        cmd.Parameters.AddWithValue("@bph", req.BadgePhys);
        cmd.Parameters.AddWithValue("@no", (object?)req.Note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@n", number);
        cmd.ExecuteNonQuery();

        return new Week
        {
            Number = number,
            Title = req.Title,
            Phase = req.Phase,
            BadgePc = req.BadgePc,
            BadgePhys = req.BadgePhys,
            Note = req.Note
        };
    }

    public void DeleteWeek(int number)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var tx = con.BeginTransaction();

        using var delTasks = con.CreateCommand();
        delTasks.CommandText = "DELETE FROM tasks WHERE week_number = @n";
        delTasks.Parameters.AddWithValue("@n", number);
        delTasks.ExecuteNonQuery();

        using var delWeek = con.CreateCommand();
        delWeek.CommandText = "DELETE FROM weeks WHERE number = @n";
        delWeek.Parameters.AddWithValue("@n", number);
        delWeek.ExecuteNonQuery();

        tx.Commit();
    }

    // ── TASKS ──

    public AppTask CreateTask(CreateTaskRequest req)
    {
        using var con = _context.CreateConnection();
        con.Open();

        using var sortCmd = con.CreateCommand();
        sortCmd.CommandText = "SELECT COALESCE(MAX(sort_order), 0) + 1 FROM tasks WHERE week_number = @w";
        sortCmd.Parameters.AddWithValue("@w", req.WeekNumber);
        var nextSort = (long)(sortCmd.ExecuteScalar() ?? 1L);

        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO tasks (week_number, sort_order, type, text, hours)
            VALUES (@w, @s, @t, @tx, @h);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@w", req.WeekNumber);
        cmd.Parameters.AddWithValue("@s", nextSort);
        cmd.Parameters.AddWithValue("@t", req.Type);
        cmd.Parameters.AddWithValue("@tx", req.Text);
        cmd.Parameters.AddWithValue("@h", req.Hours);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);

        return new AppTask
        {
            Type = req.Type,
            Text = req.Text,
            Hours = req.Hours
        };
    }

    public AppTask UpdateTask(int id, UpdateTaskRequest req)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE tasks SET type = @t, text = @tx, hours = @h WHERE id = @id";
        cmd.Parameters.AddWithValue("@t", req.Type);
        cmd.Parameters.AddWithValue("@tx", req.Text);
        cmd.Parameters.AddWithValue("@h", req.Hours);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();

        return new AppTask
        {
            Type = req.Type,
            Text = req.Text,
            Hours = req.Hours
        };
    }

    public void DeleteTask(int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM tasks WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void ReorderTasks(int weekNumber, ReorderTasksRequest req)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var tx = con.BeginTransaction();

        for (int i = 0; i < req.TaskIds.Count; i++)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = "UPDATE tasks SET sort_order = @s WHERE id = @id AND week_number = @w";
            cmd.Parameters.AddWithValue("@s", i + 1);
            cmd.Parameters.AddWithValue("@id", req.TaskIds[i]);
            cmd.Parameters.AddWithValue("@w", weekNumber);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }
}