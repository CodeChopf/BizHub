using AuraPrintsApi.Data;
using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public class InviteRepository : IInviteRepository
{
    private readonly DatabaseContext _context;

    public InviteRepository(DatabaseContext context)
    {
        _context = context;
    }

    public Invite Create(string type, int? projectId, string role, int createdBy, int hoursValid = 48)
    {
        var token     = Guid.NewGuid().ToString("N");
        var createdAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var expiresAt = DateTime.UtcNow.AddHours(hoursValid).ToString("yyyy-MM-dd HH:mm:ss");

        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO invites (token, type, project_id, role, created_by, created_at, expires_at)
            VALUES (@tok, @type, @pid, @role, @cb, @ca, @ea)";
        cmd.Parameters.AddWithValue("@tok",  token);
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@pid",  (object?)projectId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@role", role);
        cmd.Parameters.AddWithValue("@cb",   createdBy);
        cmd.Parameters.AddWithValue("@ca",   createdAt);
        cmd.Parameters.AddWithValue("@ea",   expiresAt);
        cmd.ExecuteNonQuery();

        return new Invite
        {
            Token     = token,
            Type      = type,
            ProjectId = projectId,
            Role      = role,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt
        };
    }

    public Invite? GetByToken(string token)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            SELECT i.token, i.type, i.project_id, i.role, i.created_by,
                   i.created_at, i.expires_at, i.used_at, p.name
            FROM invites i
            LEFT JOIN projects p ON p.id = i.project_id
            WHERE i.token = @tok";
        cmd.Parameters.AddWithValue("@tok", token);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new Invite
        {
            Token       = reader.GetString(0),
            Type        = reader.GetString(1),
            ProjectId   = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            Role        = reader.GetString(3),
            CreatedBy   = reader.GetInt32(4),
            CreatedAt   = reader.GetString(5),
            ExpiresAt   = reader.GetString(6),
            UsedAt      = reader.IsDBNull(7) ? null : reader.GetString(7),
            ProjectName = reader.IsDBNull(8) ? null : reader.GetString(8)
        };
    }

    public void MarkUsed(string token)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE invites SET used_at = @now WHERE token = @tok";
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@tok", token);
        cmd.ExecuteNonQuery();
    }
}
