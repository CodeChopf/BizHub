namespace AuraPrintsApi.Repositories;

public interface IStateRepository
{
    Dictionary<string, bool> GetState(int projectId);
    void SaveState(int projectId, Dictionary<string, bool> state);
}