using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface ISettingsRepository
{
    ProjectSettings GetSettings();
    void SaveSettings(ProjectSettings settings);
    bool IsSetup();
    string? GetPasswordHash();
    void SetPasswordHash(string hash);
    void DeletePasswordHash();
}