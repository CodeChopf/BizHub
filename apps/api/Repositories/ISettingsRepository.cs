using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface ISettingsRepository
{
    ProjectSettings GetSettings(int projectId);
    void SaveSettings(int projectId, ProjectSettings settings);
    bool IsSetup(int projectId);
    string? GetPasswordHash();
    void SetPasswordHash(string hash);
    void DeletePasswordHash();
}