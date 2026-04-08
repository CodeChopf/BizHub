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
        app.MapGet("/api/data", (HttpRequest req, IRoadmapRepository repo) =>
            Results.Ok(repo.GetAll(ApiHelpers.GetProjectId(req))));

        // GET /api/products (Legacy — generische Produkte)
        app.MapGet("/api/products", (IProductRepository repo) =>
            Results.Ok(repo.GetAll()));

        // GET /api/state
        app.MapGet("/api/state", (HttpRequest req, IStateRepository repo) =>
            Results.Ok(repo.GetState(ApiHelpers.GetProjectId(req))));

        // POST /api/state
        app.MapPost("/api/state", async (HttpRequest request, IStateRepository repo) =>
        {
            var state = await JsonSerializer.DeserializeAsync<Dictionary<string, bool>>(request.Body);
            if (state == null) return Results.BadRequest();
            repo.SaveState(ApiHelpers.GetProjectId(request), state);
            return Results.Ok(new { saved = true });
        });

        // ── ADMIN: WOCHEN ──

        app.MapPost("/api/admin/weeks", async (HttpRequest request, IAdminRepository repo) =>
        {
            var req = await JsonSerializer.DeserializeAsync<CreateWeekRequest>(request.Body, ApiHelpers.JsonOptions);
            if (req == null) return Results.BadRequest();
            return Results.Ok(repo.CreateWeek(ApiHelpers.GetProjectId(request), req));
        });

        app.MapPut("/api/admin/weeks/{number}", async (int number, HttpRequest request, IAdminRepository repo) =>
        {
            var req = await JsonSerializer.DeserializeAsync<UpdateWeekRequest>(request.Body, ApiHelpers.JsonOptions);
            if (req == null) return Results.BadRequest();
            return Results.Ok(repo.UpdateWeek(ApiHelpers.GetProjectId(request), number, req));
        });

        app.MapDelete("/api/admin/weeks/{number}", (int number, HttpRequest request, IAdminRepository repo) =>
        {
            repo.DeleteWeek(ApiHelpers.GetProjectId(request), number);
            return Results.Ok(new { deleted = true });
        });

        // ── ADMIN: TASKS ──

        app.MapPost("/api/admin/tasks", async (HttpRequest request, IAdminRepository repo) =>
        {
            var req = await JsonSerializer.DeserializeAsync<CreateTaskRequest>(request.Body, ApiHelpers.JsonOptions);
            if (req == null) return Results.BadRequest();
            return Results.Ok(repo.CreateTask(ApiHelpers.GetProjectId(request), req));
        });

        app.MapPut("/api/admin/tasks/{id}", async (int id, HttpRequest request, IAdminRepository repo) =>
        {
            var req = await JsonSerializer.DeserializeAsync<UpdateTaskRequest>(request.Body, ApiHelpers.JsonOptions);
            if (req == null) return Results.BadRequest();
            return Results.Ok(repo.UpdateTask(id, req));
        });

        app.MapDelete("/api/admin/tasks/{id}", (int id, IAdminRepository repo) =>
        {
            repo.DeleteTask(id);
            return Results.Ok(new { deleted = true });
        });

        app.MapPut("/api/admin/weeks/{number}/reorder", async (int number, HttpRequest request, IAdminRepository repo) =>
        {
            var req = await JsonSerializer.DeserializeAsync<ReorderTasksRequest>(request.Body, ApiHelpers.JsonOptions);
            if (req == null) return Results.BadRequest();
            repo.ReorderTasks(ApiHelpers.GetProjectId(request), number, req);
            return Results.Ok(new { reordered = true });
        });

        // ── ADMIN: SUBTASKS ──

        app.MapPost("/api/admin/subtasks", async (HttpRequest request, IAdminRepository repo) =>
        {
            var req = await JsonSerializer.DeserializeAsync<CreateSubtaskRequest>(request.Body, ApiHelpers.JsonOptions);
            if (req == null) return Results.BadRequest();
            return Results.Ok(repo.CreateSubtask(ApiHelpers.GetProjectId(request), req));
        });

        app.MapPut("/api/admin/subtasks/{id}", async (int id, HttpRequest request, IAdminRepository repo) =>
        {
            var req = await JsonSerializer.DeserializeAsync<UpdateSubtaskRequest>(request.Body, ApiHelpers.JsonOptions);
            if (req == null) return Results.BadRequest();
            return Results.Ok(repo.UpdateSubtask(id, req));
        });

        app.MapDelete("/api/admin/subtasks/{id}", (int id, IAdminRepository repo) =>
        {
            repo.DeleteSubtask(id);
            return Results.Ok(new { deleted = true });
        });

        // ── TASK TAGS ──

        app.MapGet("/api/task-tags", (HttpRequest request, ITaskTagRepository tagRepo) =>
            Results.Ok(tagRepo.GetAll(ApiHelpers.GetProjectId(request))));

        app.MapPost("/api/task-tags", async (HttpRequest request, ITaskTagRepository tagRepo) =>
        {
            var req = await JsonSerializer.DeserializeAsync<CreateTaskTagRequest>(request.Body, ApiHelpers.JsonOptions);
            if (req == null) return Results.BadRequest();
            return Results.Ok(tagRepo.Add(ApiHelpers.GetProjectId(request), req.Name, req.Color));
        });

        app.MapPut("/api/task-tags/{id}", async (int id, HttpRequest request, ITaskTagRepository tagRepo) =>
        {
            var req = await JsonSerializer.DeserializeAsync<UpdateTaskTagRequest>(request.Body, ApiHelpers.JsonOptions);
            if (req == null) return Results.BadRequest();
            return Results.Ok(tagRepo.Update(id, req.Name, req.Color));
        });

        app.MapDelete("/api/task-tags/{id}", (int id, ITaskTagRepository tagRepo) =>
        {
            tagRepo.Delete(id);
            return Results.Ok(new { deleted = true });
        });

        // GET /api/admin/tasks/ids
        app.MapGet("/api/admin/tasks/ids", (int weekNumber, HttpRequest request, DatabaseContext dbContext) =>
        {
            var projectId = ApiHelpers.GetProjectId(request);
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
