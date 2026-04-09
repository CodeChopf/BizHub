using AuraPrintsApi.Data;
using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public class AgentRepository : IAgentRepository
{
    private readonly DatabaseContext _context;

    public AgentRepository(DatabaseContext context)
    {
        _context = context;
    }

    public void RecordUsage(int userId, int projectId, int inputTokens, int outputTokens)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO agent_token_usage (user_id, project_id, input_tokens, output_tokens, recorded_at)
            VALUES (@uid, @pid, @in, @out, @ts)";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@pid", projectId);
        cmd.Parameters.AddWithValue("@in",  inputTokens);
        cmd.Parameters.AddWithValue("@out", outputTokens);
        cmd.Parameters.AddWithValue("@ts",  now);
        cmd.ExecuteNonQuery();
    }

    public (long input30d, long output30d) GetUsage30d(int userId)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            SELECT COALESCE(SUM(input_tokens), 0), COALESCE(SUM(output_tokens), 0)
            FROM agent_token_usage
            WHERE user_id = @uid AND recorded_at >= datetime('now', '-30 days')";
        cmd.Parameters.AddWithValue("@uid", userId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return (0, 0);
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    public (int inputLimit, int outputLimit) GetTierLimits(string tier)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT input_limit, output_limit FROM agent_tier_config WHERE tier = @t";
        cmd.Parameters.AddWithValue("@t", tier);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return (50000, 20000); // free defaults as fallback
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    public string GetUserTier(int userId)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT agent_tier FROM users WHERE id = @uid";
        cmd.Parameters.AddWithValue("@uid", userId);
        return (cmd.ExecuteScalar() as string) ?? "free";
    }

    public void SetUserTier(int userId, string tier)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE users SET agent_tier = @t WHERE id = @uid";
        cmd.Parameters.AddWithValue("@t",   tier);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.ExecuteNonQuery();
    }

    public List<AgentUsageSummary> GetAllUsageSummaries()
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            SELECT
                u.id,
                u.username,
                u.agent_tier,
                COALESCE(SUM(CASE WHEN atu.recorded_at >= datetime('now', '-30 days') THEN atu.input_tokens  ELSE 0 END), 0) AS input30d,
                COALESCE(SUM(CASE WHEN atu.recorded_at >= datetime('now', '-30 days') THEN atu.output_tokens ELSE 0 END), 0) AS output30d,
                COALESCE(tc.input_limit,  50000) AS input_limit,
                COALESCE(tc.output_limit, 20000) AS output_limit
            FROM users u
            LEFT JOIN agent_token_usage atu ON atu.user_id = u.id
            LEFT JOIN agent_tier_config tc  ON tc.tier = u.agent_tier
            GROUP BY u.id, u.username, u.agent_tier, tc.input_limit, tc.output_limit
            ORDER BY u.id";
        var list = new List<AgentUsageSummary>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(new AgentUsageSummary
            {
                UserId         = reader.GetInt32(0),
                Username       = reader.GetString(1),
                Tier           = reader.GetString(2),
                InputTokens30d = reader.GetInt64(3),
                OutputTokens30d = reader.GetInt64(4),
                LimitInput     = reader.GetInt32(5),
                LimitOutput    = reader.GetInt32(6)
            });
        return list;
    }
}
