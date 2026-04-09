using AuraPrintsApi.Data;
using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public class ActivityRepository : IActivityRepository
{
    private readonly DatabaseContext _context;

    public ActivityRepository(DatabaseContext context)
    {
        _context = context;
    }

    public List<ActivityEntry> GetRecent(int projectId, int limit = 20)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            SELECT id, project_id, entity_type, action, title, description, actor, created_at
            FROM activity_log
            WHERE project_id = @pid
            ORDER BY created_at DESC, id DESC
            LIMIT @limit";
        cmd.Parameters.AddWithValue("@pid", projectId);
        cmd.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 100));

        var list = new List<ActivityEntry>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new ActivityEntry
            {
                Id = reader.GetInt64(0),
                ProjectId = reader.GetInt32(1),
                EntityType = reader.GetString(2),
                Action = reader.GetString(3),
                Title = reader.GetString(4),
                Description = reader.IsDBNull(5) ? null : reader.GetString(5),
                Actor = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt = reader.GetString(7)
            });
        }

        return list;
    }

    public void Add(int projectId, string entityType, string action, string title, string? description, string? actor)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO activity_log (project_id, entity_type, action, title, description, actor, created_at)
            VALUES (@pid, @entityType, @action, @title, @description, @actor, @createdAt)";
        cmd.Parameters.AddWithValue("@pid", projectId);
        cmd.Parameters.AddWithValue("@entityType", entityType);
        cmd.Parameters.AddWithValue("@action", action);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@description", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@actor", (object?)actor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
    }
}
