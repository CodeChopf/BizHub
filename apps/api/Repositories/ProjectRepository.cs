using AuraPrintsApi.Data;
using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly DatabaseContext _context;

    public ProjectRepository(DatabaseContext context)
    {
        _context = context;
    }

    public List<Project> GetForUser(int userId)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            SELECT p.id, p.name, p.description, p.start_date, p.currency,
                   p.project_image, p.visible_tabs, p.created_at,
                   COALESCE(pm.role, 'admin') as role
            FROM projects p
            LEFT JOIN project_members pm ON pm.project_id = p.id AND pm.user_id = @uid
            WHERE pm.user_id = @uid
               OR EXISTS (SELECT 1 FROM users WHERE id = @uid AND is_admin = 1)
            ORDER BY p.id";
        cmd.Parameters.AddWithValue("@uid", userId);
        var result = new List<Project>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Project
            {
                Id           = reader.GetInt32(0),
                Name         = reader.GetString(1),
                Description  = reader.IsDBNull(2) ? null : reader.GetString(2),
                StartDate    = reader.IsDBNull(3) ? null : reader.GetString(3),
                Currency     = reader.GetString(4),
                ProjectImage = reader.IsDBNull(5) ? null : reader.GetString(5),
                VisibleTabs  = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt    = reader.GetString(7),
                Role         = reader.GetString(8)
            });
        }
        return result;
    }

    public Project? GetById(int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            SELECT id, name, description, start_date, currency, project_image, visible_tabs, created_at
            FROM projects WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new Project
        {
            Id           = reader.GetInt32(0),
            Name         = reader.GetString(1),
            Description  = reader.IsDBNull(2) ? null : reader.GetString(2),
            StartDate    = reader.IsDBNull(3) ? null : reader.GetString(3),
            Currency     = reader.GetString(4),
            ProjectImage = reader.IsDBNull(5) ? null : reader.GetString(5),
            VisibleTabs  = reader.IsDBNull(6) ? null : reader.GetString(6),
            CreatedAt    = reader.GetString(7)
        };
    }

    public Project Create(string name, string? description, string? startDate, string currency, int adminUserId)
    {
        var createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        using var con = _context.CreateConnection();
        con.Open();
        using var tx = con.BeginTransaction();

        using var pCmd = con.CreateCommand();
        pCmd.CommandText = @"
            INSERT INTO projects (name, description, start_date, currency, created_at)
            VALUES (@n, @d, @sd, @cur, @ca);
            SELECT last_insert_rowid();";
        pCmd.Parameters.AddWithValue("@n",   name);
        pCmd.Parameters.AddWithValue("@d",   (object?)description ?? DBNull.Value);
        pCmd.Parameters.AddWithValue("@sd",  (object?)startDate ?? DBNull.Value);
        pCmd.Parameters.AddWithValue("@cur", currency);
        pCmd.Parameters.AddWithValue("@ca",  createdAt);
        var id = (long)(pCmd.ExecuteScalar() ?? 0L);

        using var mCmd = con.CreateCommand();
        mCmd.CommandText = "INSERT INTO project_members (project_id, user_id, role) VALUES (@pid, @uid, 'admin')";
        mCmd.Parameters.AddWithValue("@pid", id);
        mCmd.Parameters.AddWithValue("@uid", adminUserId);
        mCmd.ExecuteNonQuery();

        // settings_v2 mit Anfangswerten befüllen
        var initialSettings = new Dictionary<string, string> { ["project_name"] = name, ["currency"] = currency };
        if (!string.IsNullOrEmpty(description)) initialSettings["description"] = description;
        if (!string.IsNullOrEmpty(startDate))   initialSettings["start_date"]  = startDate;
        foreach (var (key, value) in initialSettings)
        {
            using var sCmd = con.CreateCommand();
            sCmd.CommandText = @"
                INSERT OR IGNORE INTO settings_v2 (project_id, key, value)
                VALUES (@pid, @k, @v)";
            sCmd.Parameters.AddWithValue("@pid", id);
            sCmd.Parameters.AddWithValue("@k", key);
            sCmd.Parameters.AddWithValue("@v", value);
            sCmd.ExecuteNonQuery();
        }

        tx.Commit();

        return new Project
        {
            Id          = (int)id,
            Name        = name,
            Description = description,
            StartDate   = startDate,
            Currency    = currency,
            CreatedAt   = createdAt,
            Role        = "admin"
        };
    }

    public void Update(int id, string name, string? description, string? startDate, string currency, string? projectImage, string? visibleTabs)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            UPDATE projects SET name = @n, description = @d, start_date = @sd,
                currency = @cur, project_image = @img, visible_tabs = @tabs
            WHERE id = @id";
        cmd.Parameters.AddWithValue("@n",    name);
        cmd.Parameters.AddWithValue("@d",    (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sd",   (object?)startDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cur",  currency);
        cmd.Parameters.AddWithValue("@img",  (object?)projectImage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@tabs", (object?)visibleTabs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id",   id);
        cmd.ExecuteNonQuery();
    }

    public List<ProjectMember> GetMembers(int projectId)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            SELECT pm.project_id, pm.user_id, u.username, pm.role
            FROM project_members pm
            JOIN users u ON u.id = pm.user_id
            WHERE pm.project_id = @pid
            ORDER BY pm.role DESC, u.username";
        cmd.Parameters.AddWithValue("@pid", projectId);
        var result = new List<ProjectMember>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new ProjectMember
            {
                ProjectId = reader.GetInt32(0),
                UserId    = reader.GetInt32(1),
                Username  = reader.GetString(2),
                Role      = reader.GetString(3)
            });
        }
        return result;
    }

    public void AddMember(int projectId, int userId, string role)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO project_members (project_id, user_id, role)
            VALUES (@pid, @uid, @role)";
        cmd.Parameters.AddWithValue("@pid",  projectId);
        cmd.Parameters.AddWithValue("@uid",  userId);
        cmd.Parameters.AddWithValue("@role", role);
        cmd.ExecuteNonQuery();
    }

    public void RemoveMember(int projectId, int userId)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM project_members WHERE project_id = @pid AND user_id = @uid";
        cmd.Parameters.AddWithValue("@pid", projectId);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.ExecuteNonQuery();
    }

    public bool IsMember(int projectId, int userId)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM project_members WHERE project_id = @pid AND user_id = @uid";
        cmd.Parameters.AddWithValue("@pid", projectId);
        cmd.Parameters.AddWithValue("@uid", userId);
        return (long)(cmd.ExecuteScalar() ?? 0L) > 0;
    }

    public string? GetRole(int projectId, int userId)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT role FROM project_members WHERE project_id = @pid AND user_id = @uid";
        cmd.Parameters.AddWithValue("@pid", projectId);
        cmd.Parameters.AddWithValue("@uid", userId);
        return cmd.ExecuteScalar() as string;
    }
}
