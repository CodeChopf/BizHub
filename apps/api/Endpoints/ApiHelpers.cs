using System.Text.Json;
using AuraPrintsApi.Repositories;

namespace AuraPrintsApi.Endpoints;

public static class ApiHelpers
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static bool TryGetProjectId(HttpRequest req, out int projectId)
    {
        projectId = 0;
        return req.Query.TryGetValue("projectId", out var p) && int.TryParse(p, out projectId) && projectId > 0;
    }

    public static int GetProjectId(HttpRequest req) =>
        TryGetProjectId(req, out var projectId) ? projectId : 0;

    public static IResult? EnsureProjectMember(HttpRequest req, HttpContext ctx, IUserRepository userRepo, IProjectRepository projectRepo, out int projectId)
    {
        projectId = 0;
        var username = ctx.User.Identity?.Name ?? string.Empty;
        var user = userRepo.GetByUsername(username);
        if (user == null) return Results.Unauthorized();
        if (!TryGetProjectId(req, out projectId)) return Results.BadRequest(new { error = "Missing or invalid projectId query parameter." });
        if (!projectRepo.IsMember(projectId, user.Id)) return Results.Forbid();
        return null;
    }

    public static IResult? EnsureProjectAdmin(HttpRequest req, HttpContext ctx, IUserRepository userRepo, IProjectRepository projectRepo, out int projectId)
    {
        var memberResult = EnsureProjectMember(req, ctx, userRepo, projectRepo, out projectId);
        if (memberResult != null) return memberResult;

        var username = ctx.User.Identity?.Name ?? string.Empty;
        var user = userRepo.GetByUsername(username);
        if (user == null) return Results.Unauthorized();
        return projectRepo.GetRole(projectId, user.Id) == "admin" ? null : Results.Forbid();
    }
}
