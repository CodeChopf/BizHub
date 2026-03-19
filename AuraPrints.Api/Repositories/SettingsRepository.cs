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

    public bool IsSetup()
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM settings WHERE key = 'project_name'";
        var count = (long)(cmd.ExecuteScalar() ?? 0L);
        return count > 0;
    }

    public ProjectSettings GetSettings()
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM settings";
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
            IsSetup = dict.ContainsKey("project_name")
        };
    }

    public void SaveSettings(ProjectSettings settings)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var tx = con.BeginTransaction();

        var values = new Dictionary<string, string>
        {
            ["project_name"] = settings.ProjectName,
            ["start_date"] = settings.StartDate,
            ["description"] = settings.Description,
            ["currency"] = settings.Currency
        };

        foreach (var (key, value) in values)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO settings (key, value) VALUES (@k, @v)
                ON CONFLICT(key) DO UPDATE SET value = @v";
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@v", value);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }
}