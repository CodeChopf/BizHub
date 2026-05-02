using AuraPrintsApi.Repositories;

namespace AuraPrintsApi.Endpoints;

public static class ActivityEndpoints
{
    public static WebApplication MapActivityEndpoints(this WebApplication app)
    {
        app.MapGet("/api/activity", (HttpRequest req, HttpContext ctx, IActivityRepository repo, int? limit, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(req, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            return Results.Ok(repo.GetRecent(projectId, limit ?? 20));
        });

        return app;
    }
}
