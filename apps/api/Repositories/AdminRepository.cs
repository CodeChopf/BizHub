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

    public Week CreateWeek(int projectId, CreateWeekRequest req)
    {
        using var con = _context.CreateConnection();
        con.Open();

        // Nächste Wochennummer ermitteln (pro Projekt)
        using var numCmd = con.CreateCommand();
        numCmd.CommandText = "SELECT COALESCE(MAX(number), 0) + 1 FROM weeks WHERE project_id = @pid";
        numCmd.Parameters.AddWithValue("@pid", projectId);
        var nextNumber = (long)(numCmd.ExecuteScalar() ?? 1L);

        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO weeks (number, title, phase, badge_pc, badge_phys, note, project_id)
            VALUES (@n, @t, @p, @bp, @bph, @no, @pid)";
        cmd.Parameters.AddWithValue("@n",   nextNumber);
        cmd.Parameters.AddWithValue("@t",   req.Title);
        cmd.Parameters.AddWithValue("@p",   req.Phase);
        cmd.Parameters.AddWithValue("@bp",  req.BadgePc);
        cmd.Parameters.AddWithValue("@bph", req.BadgePhys);
        cmd.Parameters.AddWithValue("@no",  (object?)req.Note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pid", projectId);
        cmd.ExecuteNonQuery();

        return new Week
        {
            Number    = (int)nextNumber,
            Title     = req.Title,
            Phase     = req.Phase,
            BadgePc   = req.BadgePc,
            BadgePhys = req.BadgePhys,
            Note      = req.Note
        };
    }

    public Week UpdateWeek(int projectId, int number, UpdateWeekRequest req)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            UPDATE weeks SET title = @t, phase = @p, badge_pc = @bp, badge_phys = @bph, note = @no
            WHERE number = @n AND project_id = @pid";
        cmd.Parameters.AddWithValue("@t",   req.Title);
        cmd.Parameters.AddWithValue("@p",   req.Phase);
        cmd.Parameters.AddWithValue("@bp",  req.BadgePc);
        cmd.Parameters.AddWithValue("@bph", req.BadgePhys);
        cmd.Parameters.AddWithValue("@no",  (object?)req.Note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@n",   number);
        cmd.Parameters.AddWithValue("@pid", projectId);
        cmd.ExecuteNonQuery();

        return new Week
        {
            Number    = number,
            Title     = req.Title,
            Phase     = req.Phase,
            BadgePc   = req.BadgePc,
            BadgePhys = req.BadgePhys,
            Note      = req.Note
        };
    }

    public void DeleteWeek(int projectId, int number)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var tx = con.BeginTransaction();

        using var delSubs = con.CreateCommand();
        delSubs.CommandText = @"
            DELETE FROM subtasks WHERE task_id IN (
                SELECT t.id FROM tasks t
                JOIN weeks w ON w.number = t.week_number AND w.project_id = @pid
                WHERE t.week_number = @n
            )";
        delSubs.Parameters.AddWithValue("@n",   number);
        delSubs.Parameters.AddWithValue("@pid", projectId);
        delSubs.ExecuteNonQuery();

        using var delTasks = con.CreateCommand();
        delTasks.CommandText = @"
            DELETE FROM tasks WHERE week_number = @n
            AND EXISTS (SELECT 1 FROM weeks WHERE number = @n AND project_id = @pid)";
        delTasks.Parameters.AddWithValue("@n",   number);
        delTasks.Parameters.AddWithValue("@pid", projectId);
        delTasks.ExecuteNonQuery();

        using var delWeek = con.CreateCommand();
        delWeek.CommandText = "DELETE FROM weeks WHERE number = @n AND project_id = @pid";
        delWeek.Parameters.AddWithValue("@n",   number);
        delWeek.Parameters.AddWithValue("@pid", projectId);
        delWeek.ExecuteNonQuery();

        tx.Commit();
    }

    // ── TASKS ──

    public AppTask CreateTask(int projectId, CreateTaskRequest req)
    {
        using var con = _context.CreateConnection();
        con.Open();

        using var sortCmd = con.CreateCommand();
        sortCmd.CommandText = @"
            SELECT COALESCE(MAX(t.sort_order), 0) + 1
            FROM tasks t
            JOIN weeks w ON w.number = t.week_number AND w.project_id = @pid
            WHERE t.week_number = @w";
        sortCmd.Parameters.AddWithValue("@pid", projectId);
        sortCmd.Parameters.AddWithValue("@w", req.WeekNumber);
        var nextSort = (long)(sortCmd.ExecuteScalar() ?? 1L);

        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO tasks (week_number, sort_order, type, text, hours, project_id)
            VALUES (@w, @s, @t, @tx, @h, @pid);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@w",   req.WeekNumber);
        cmd.Parameters.AddWithValue("@s",   nextSort);
        cmd.Parameters.AddWithValue("@t",   req.Type);
        cmd.Parameters.AddWithValue("@tx",  req.Text);
        cmd.Parameters.AddWithValue("@h",   req.Hours);
        cmd.Parameters.AddWithValue("@pid", projectId);
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
        using var tx = con.BeginTransaction();
        using var delSub = con.CreateCommand();
        delSub.CommandText = "DELETE FROM subtasks WHERE task_id = @id";
        delSub.Parameters.AddWithValue("@id", id);
        delSub.ExecuteNonQuery();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM tasks WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    public void ReorderTasks(int projectId, int weekNumber, ReorderTasksRequest req)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var tx = con.BeginTransaction();

        for (int i = 0; i < req.TaskIds.Count; i++)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
                UPDATE tasks SET sort_order = @s
                WHERE id = @id AND week_number = @w
                AND EXISTS (SELECT 1 FROM weeks WHERE number = @w AND project_id = @pid)";
            cmd.Parameters.AddWithValue("@s",   i + 1);
            cmd.Parameters.AddWithValue("@id",  req.TaskIds[i]);
            cmd.Parameters.AddWithValue("@w",   weekNumber);
            cmd.Parameters.AddWithValue("@pid", projectId);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    // ── SUBTASKS ──

    public AppSubtask CreateSubtask(int projectId, CreateSubtaskRequest req)
    {
        using var con = _context.CreateConnection();
        con.Open();

        using var sortCmd = con.CreateCommand();
        sortCmd.CommandText = "SELECT COALESCE(MAX(sort_order), 0) + 1 FROM subtasks WHERE task_id = @tid";
        sortCmd.Parameters.AddWithValue("@tid", req.TaskId);
        var nextSort = (long)(sortCmd.ExecuteScalar() ?? 1L);

        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO subtasks (task_id, sort_order, text, hours, project_id)
            VALUES (@tid, @s, @tx, @h, @pid);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@tid", req.TaskId);
        cmd.Parameters.AddWithValue("@s",   nextSort);
        cmd.Parameters.AddWithValue("@tx",  req.Text);
        cmd.Parameters.AddWithValue("@h",   req.Hours);
        cmd.Parameters.AddWithValue("@pid", projectId);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);

        return new AppSubtask { Id = (int)id, Text = req.Text, Hours = req.Hours };
    }

    public AppSubtask UpdateSubtask(int id, UpdateSubtaskRequest req)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE subtasks SET text = @tx, hours = @h WHERE id = @id";
        cmd.Parameters.AddWithValue("@tx", req.Text);
        cmd.Parameters.AddWithValue("@h",  req.Hours);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();

        return new AppSubtask { Id = id, Text = req.Text, Hours = req.Hours };
    }

    public void DeleteSubtask(int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM subtasks WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }
}