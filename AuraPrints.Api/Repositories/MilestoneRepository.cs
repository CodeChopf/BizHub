using AuraPrintsApi.Data;
using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public class MilestoneRepository : IMilestoneRepository
{
    private readonly DatabaseContext _context;

    public MilestoneRepository(DatabaseContext context)
    {
        _context = context;
    }

    public List<MilestoneListItem> GetAll()
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT id, name, description, created_at FROM milestones ORDER BY id DESC";
        var result = new List<MilestoneListItem>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new MilestoneListItem
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                CreatedAt = reader.GetString(3)
            });
        }
        return result;
    }

    public Milestone GetById(int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT id, name, description, created_at, snapshot FROM milestones WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) throw new KeyNotFoundException($"Milestone {id} not found");
        return new Milestone
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Description = reader.IsDBNull(2) ? null : reader.GetString(2),
            CreatedAt = reader.GetString(3),
            Snapshot = reader.GetString(4)
        };
    }

    public Milestone Create(string name, string? description, string snapshot)
    {
        using var con = _context.CreateConnection();
        con.Open();
        var createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO milestones (name, description, created_at, snapshot)
            VALUES (@n, @d, @ca, @s);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@d", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ca", createdAt);
        cmd.Parameters.AddWithValue("@s", snapshot);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);

        return new Milestone
        {
            Id = (int)id,
            Name = name,
            Description = description,
            CreatedAt = createdAt,
            Snapshot = snapshot
        };
    }

    public void Delete(int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM milestones WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }
}