using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IAgentRepository
{
    void RecordUsage(int userId, int projectId, int inputTokens, int outputTokens);
    (long input30d, long output30d) GetUsage30d(int userId);
    (int inputLimit, int outputLimit) GetTierLimits(string tier);
    string GetUserTier(int userId);
    void SetUserTier(int userId, string tier);
    List<AgentUsageSummary> GetAllUsageSummaries();
}
