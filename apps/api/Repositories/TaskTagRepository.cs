using AuraPrintsApi.Data;
using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public class TaskTagRepository : ITaskTagRepository
{
    private readonly DatabaseContext _context;

    public TaskTagRepository(DatabaseContext context)
    {
        _context = context;
    }

    public List<TaskTag> GetAll(int projectId)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT id, name, color FROM task_tags WHERE project_id = @pid ORDER BY sort_order, name";
        cmd.Parameters.AddWithValue("@pid", projectId);
        var result = new List<TaskTag>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new TaskTag
            {
                Id    = reader.GetInt32(0),
                Name  = reader.GetString(1),
                Color = reader.GetString(2)
            });
        }
        return result;
    }

    public TaskTag Add(int projectId, string name, string color)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "INSERT INTO task_tags (project_id, name, color) VALUES (@pid, @n, @c); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@pid", projectId);
        cmd.Parameters.AddWithValue("@n",   name);
        cmd.Parameters.AddWithValue("@c",   color);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        return new TaskTag { Id = (int)id, Name = name, Color = color };
    }

    public TaskTag Update(int id, string name, string color)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE task_tags SET name = @n, color = @c WHERE id = @id";
        cmd.Parameters.AddWithValue("@n",   name);
        cmd.Parameters.AddWithValue("@c",   color);
        cmd.Parameters.AddWithValue("@id",  id);
        cmd.ExecuteNonQuery();
        return new TaskTag { Id = id, Name = name, Color = color };
    }

    public void Delete(int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var tx = con.BeginTransaction();
        using var delAssign = con.CreateCommand();
        delAssign.CommandText = "DELETE FROM task_tag_assignments WHERE tag_id = @id";
        delAssign.Parameters.AddWithValue("@id", id);
        delAssign.ExecuteNonQuery();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM task_tags WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
        tx.Commit();
    }
}
