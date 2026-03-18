namespace AuraPrintsApi.Repositories;

public interface IStateRepository
{
    Dictionary<string, bool> GetState();
    void SaveState(Dictionary<string, bool> state);
}