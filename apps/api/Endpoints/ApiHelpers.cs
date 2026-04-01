using System.Text.Json;

namespace AuraPrintsApi.Endpoints;

public static class ApiHelpers
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static int GetProjectId(HttpRequest req) =>
        req.Query.TryGetValue("projectId", out var p) && int.TryParse(p, out var pid) ? pid : 1;
}
