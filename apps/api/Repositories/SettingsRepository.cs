using AuraPrintsApi.Data;
using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public class SettingsRepository : ISettingsRepository
{
    private readonly DatabaseContext _context;

    public SettingsRepository(DatabaseContext context)
    {
        _context = context;
    }

    public bool IsSetup(int projectId)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM settings_v2 WHERE project_id = @pid AND key = 'project_name'";
        cmd.Parameters.AddWithValue("@pid", projectId);
        var count = (long)(cmd.ExecuteScalar() ?? 0L);
        return count > 0;
    }

    public ProjectSettings GetSettings(int projectId)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM settings_v2 WHERE project_id = @pid";
        cmd.Parameters.AddWithValue("@pid", projectId);
        var dict = new Dictionary<string, string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            dict[reader.GetString(0)] = reader.GetString(1);

        return new ProjectSettings
        {
            ProjectName = dict.GetValueOrDefault("project_name", ""),
            StartDate = dict.GetValueOrDefault("start_date", ""),
            Description = dict.GetValueOrDefault("description", ""),
            Currency = dict.GetValueOrDefault("currency", "CHF"),
            ProjectImage = dict.ContainsKey("project_image") ? dict["project_image"] : null,
            VisibleTabs = dict.ContainsKey("visible_tabs") ? dict["visible_tabs"] : null,
            IsSetup = dict.ContainsKey("project_name")
        };
    }

    public string? GetPasswordHash()
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = 'admin_password_hash'";
        var result = cmd.ExecuteScalar();
        return result as string;
    }

    public void SetPasswordHash(string hash)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO settings (key, value) VALUES ('admin_password_hash', @hash)
            ON CONFLICT(key) DO UPDATE SET value = @hash";
        cmd.Parameters.AddWithValue("@hash", hash);
        cmd.ExecuteNonQuery();
    }

    public void DeletePasswordHash()
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM settings WHERE key = 'admin_password_hash'";
        cmd.ExecuteNonQuery();
    }

    public void SaveSettings(int projectId, ProjectSettings settings)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var tx = con.BeginTransaction();

        var values = new Dictionary<string, string?>
        {
            ["project_name"] = settings.ProjectName,
            ["start_date"] = settings.StartDate,
            ["description"] = settings.Description,
            ["currency"] = settings.Currency,
            ["project_image"] = settings.ProjectImage,
            ["visible_tabs"] = settings.VisibleTabs
        };

        foreach (var (key, value) in values)
        {
            if (value == null)
            {
                using var delCmd = con.CreateCommand();
                delCmd.CommandText = "DELETE FROM settings_v2 WHERE project_id = @pid AND key = @k";
                delCmd.Parameters.AddWithValue("@pid", projectId);
                delCmd.Parameters.AddWithValue("@k", key);
                delCmd.ExecuteNonQuery();
                continue;
            }
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
            INSERT INTO settings_v2 (project_id, key, value) VALUES (@pid, @k, @v)
            ON CONFLICT(project_id, key) DO UPDATE SET value = @v";
            cmd.Parameters.AddWithValue("@pid", projectId);
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@v", value);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }
}