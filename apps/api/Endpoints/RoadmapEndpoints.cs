using System.Text.Json;
using AuraPrintsApi.Data;
using AuraPrintsApi.Models;
using AuraPrintsApi.Repositories;

namespace AuraPrintsApi.Endpoints;

public static class RoadmapEndpoints
{
    public static WebApplication MapRoadmapEndpoints(this WebApplication app)
    {
        // GET /api/data
        app.MapGet("/api/data", (HttpRequest req, HttpContext ctx, IRoadmapRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(req, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            return Results.Ok(repo.GetAll(projectId));
        });

        // GET /api/products (Legacy — generische Produkte)
        app.MapGet("/api/products", (IProductRepository repo) =>
            Results.Ok(repo.GetAll()));

        // GET /api/state
        app.MapGet("/api/state", (HttpRequest req, HttpContext ctx, IStateRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(req, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            return Results.Ok(repo.GetState(projectId));
        });

        // POST /api/state
        app.MapPost("/api/state", async (HttpRequest request, HttpContext ctx, IStateRepository repo, IActivityRepository activityRepo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            var state = await JsonSerializer.DeserializeAsync<Dictionary<string, bool>>(request.Body);
            if (state == null) return Results.BadRequest();
            repo.SaveState(projectId, state);
            var doneCount = state.Count(kv => kv.Value);
            activityRepo.Add(projectId, "task", "state_saved", "Task-Status aktualisiert",
                $"{doneCount} Aufgaben als erledigt markiert.", request.HttpContext.User?.Identity?.Name);
            return Results.Ok(new { saved = true });
        });

        // ── ADMIN: WOCHEN ──

        app.MapPost("/api/admin/weeks", async (HttpRequest request, HttpContext ctx, IAdminRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectAdmin(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            var req = await JsonSerializer.DeserializeAsync<CreateWeekRequest>(request.Body, ApiHelpers.JsonOptions);
            if (req == null) return Results.BadRequest();
            return Results.Ok(repo.CreateWeek(projectId, req));
        });

        app.MapPut("/api/admin/weeks/{number}", async (int number, HttpRequest request, HttpContext ctx, IAdminRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectAdmin(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            var req = await JsonSerializer.DeserializeAsync<UpdateWeekRequest>(request.Body, ApiHelpers.JsonOptions);
            if (req == null) return Results.BadRequest();
            return Results.Ok(repo.UpdateWeek(projectId, number, req));
        });

        app.MapDelete("/api/admin/weeks/{number}", (int number, HttpRequest request, HttpContext ctx, IAdminRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectAdmin(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            repo.DeleteWeek(projectId, number);
            return Results.Ok(new { deleted = true });
        });

        // ── ADMIN: TASKS ──

        app.MapPost("/api/admin/tasks", async (HttpRequest request, HttpContext ctx, IAdminRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectAdmin(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            var req = await JsonSerializer.DeserializeAsync<CreateTaskRequest>(request.Body, ApiHelpers.JsonOptions);
            if (req == null) return Results.BadRequest();
            return Results.Ok(repo.CreateTask(projectId, req));
        });

        app.MapPut("/api/admin/tasks/{id}", async (int id, HttpRequest request, HttpContext ctx, IAdminRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectAdmin(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            var req = await JsonSerializer.DeserializeAsync<UpdateTaskRequest>(request.Body, ApiHelpers.JsonOptions);
            if (req == null) return Results.BadRequest();
            return Results.Ok(repo.UpdateTask(projectId, id, req));
        });

        app.MapDelete("/api/admin/tasks/{id}", (int id, HttpRequest request, HttpContext ctx, IAdminRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectAdmin(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            repo.DeleteTask(projectId, id);
            return Results.Ok(new { deleted = true });
        });

        app.MapPut("/api/admin/weeks/{number}/reorder", async (int number, HttpRequest request, HttpContext ctx, IAdminRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectAdmin(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            var req = await JsonSerializer.DeserializeAsync<ReorderTasksRequest>(request.Body, ApiHelpers.JsonOptions);
            if (req == null) return Results.BadRequest();
            repo.ReorderTasks(projectId, number, req);
            return Results.Ok(new { reordered = true });
        });

        // GET /api/admin/tasks/ids
        app.MapGet("/api/admin/tasks/ids", (int weekNumber, HttpRequest request, HttpContext ctx, DatabaseContext dbContext, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectAdmin(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            using var con = dbContext.CreateConnection();
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT t.id FROM tasks t
                WHERE t.project_id = @pid AND t.week_number = @w
                ORDER BY t.sort_order";
            cmd.Parameters.AddWithValue("@pid", projectId);
            cmd.Parameters.AddWithValue("@w", weekNumber);
            var ids = new List<int>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) ids.Add(reader.GetInt32(0));
            return Results.Ok(ids);
        });

        return app;
    }
}
