using AuraPrintsApi.Repositories;

namespace AuraPrintsApi.Endpoints;

public static class ActivityEndpoints
{
    public static WebApplication MapActivityEndpoints(this WebApplication app)
    {
        app.MapGet("/api/activity", (HttpRequest req, IActivityRepository repo, int? limit) =>
        {
            var projectId = ApiHelpers.GetProjectId(req);
            return Results.Ok(repo.GetRecent(projectId, limit ?? 20));
        });

        return app;
    }
}
