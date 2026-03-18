using AuraPrintsApi.Data;

namespace AuraPrintsApi.Repositories;

public class StateRepository : IStateRepository
{
    private readonly DatabaseContext _context;

    public StateRepository(DatabaseContext context)
    {
        _context = context;
    }

    public Dictionary<string, bool> GetState()
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM state";
        var result = new Dictionary<string, bool>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result[reader.GetString(0)] = reader.GetInt32(1) == 1;
        return result;
    }

    public void SaveState(Dictionary<string, bool> state)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var tx = con.BeginTransaction();
        foreach (var (key, value) in state)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO state (key, value) VALUES (@k, @v)
                ON CONFLICT(key) DO UPDATE SET value = @v";
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@v", value ? 1 : 0);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }
}