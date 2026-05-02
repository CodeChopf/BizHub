using System.Text.Json;
using AuraPrintsApi.Repositories;

namespace AuraPrintsApi.Endpoints;

public static class FinanceEndpoints
{
    public static WebApplication MapFinanceEndpoints(this WebApplication app)
    {
        // GET /api/finance
        app.MapGet("/api/finance", (HttpRequest req, HttpContext ctx, IExpenseRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(req, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            return Results.Ok(repo.GetAll(projectId));
        });

        // POST /api/expenses
        app.MapPost("/api/expenses", async (HttpRequest request, HttpContext ctx, IExpenseRepository repo, IActivityRepository activityRepo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            var categoryId = body.GetProperty("categoryId").GetInt32();
            var amount = body.GetProperty("amount").GetDecimal();
            var description = body.GetProperty("description").GetString() ?? "";
            var link = body.TryGetProperty("link", out var l) ? l.GetString() : null;
            var date = body.GetProperty("date").GetString() ?? DateTime.Today.ToString("yyyy-MM-dd");
            var weekNumber = body.TryGetProperty("weekNumber", out var w) && w.ValueKind != JsonValueKind.Null ? w.GetInt32() : (int?)null;
            var taskId = body.TryGetProperty("taskId", out var t) && t.ValueKind != JsonValueKind.Null ? t.GetInt32() : (int?)null;
            var type = body.TryGetProperty("type", out var tp) ? tp.GetString() ?? "expense" : "expense";
            var expense = repo.Add(projectId, categoryId, amount, description, link, date, weekNumber, taskId, type);
            activityRepo.Add(projectId, "finance", "created",
                type == "income" ? "Einnahme erfasst" : "Ausgabe erfasst",
                description, request.HttpContext.User?.Identity?.Name);
            return Results.Ok(expense);
        });

        // DELETE /api/expenses/{id}
        app.MapDelete("/api/expenses/{id}", (int id, HttpRequest request, HttpContext ctx, IExpenseRepository repo, IActivityRepository activityRepo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            repo.Delete(projectId, id);
            activityRepo.Add(projectId, "finance", "deleted", "Finanz-Eintrag gelöscht", $"ID {id}", request.HttpContext.User?.Identity?.Name);
            return Results.Ok(new { deleted = true });
        });

        // GET /api/categories
        app.MapGet("/api/categories", (HttpRequest req, HttpContext ctx, ICategoryRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(req, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            return Results.Ok(repo.GetAll(projectId));
        });

        // POST /api/categories
        app.MapPost("/api/categories", async (HttpRequest request, HttpContext ctx, ICategoryRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            var name = body.GetProperty("name").GetString() ?? "";
            var color = body.TryGetProperty("color", out var c) ? c.GetString() ?? "#4f8ef7" : "#4f8ef7";
            return Results.Ok(repo.Add(projectId, name, color));
        });

        // PUT /api/categories/{id}
        app.MapPut("/api/categories/{id}", async (int id, HttpRequest request, HttpContext ctx, ICategoryRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            var name = body.GetProperty("name").GetString() ?? "";
            var color = body.TryGetProperty("color", out var c) ? c.GetString() ?? "#4f8ef7" : "#4f8ef7";
            return Results.Ok(repo.Update(projectId, id, name, color));
        });

        // DELETE /api/categories/{id}
        app.MapDelete("/api/categories/{id}", (int id, HttpRequest request, HttpContext ctx, ICategoryRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            repo.Delete(projectId, id);
            return Results.Ok(new { deleted = true });
        });

        // GET /api/expenses/{id}/attachments
        app.MapGet("/api/expenses/{id}/attachments", (int id, HttpRequest request, HttpContext ctx, IAttachmentRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            return Results.Ok(repo.GetByExpenseId(projectId, id));
        });

        // POST /api/expenses/{id}/attachments
        app.MapPost("/api/expenses/{id}/attachments", async (int id, HttpRequest request, HttpContext ctx, IAttachmentRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            var fileName = body.GetProperty("fileName").GetString() ?? "beleg";
            var mimeType = body.GetProperty("mimeType").GetString() ?? "image/jpeg";
            var data = body.GetProperty("data").GetString() ?? "";
            if (string.IsNullOrEmpty(data)) return Results.BadRequest();
            var attachment = repo.Add(projectId, id, fileName, mimeType, data);
            return Results.Ok(attachment);
        });

        // DELETE /api/attachments/{id}
        app.MapDelete("/api/attachments/{id}", (int id, HttpRequest request, HttpContext ctx, IAttachmentRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            repo.Delete(projectId, id);
            return Results.Ok(new { deleted = true });
        });

        // PUT /api/expenses/{id}
        app.MapPut("/api/expenses/{id}", async (int id, HttpRequest request, HttpContext ctx, IExpenseRepository repo, IActivityRepository activityRepo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            var categoryId = body.GetProperty("categoryId").GetInt32();
            var amount = body.GetProperty("amount").GetDecimal();
            var description = body.GetProperty("description").GetString() ?? "";
            var link = body.TryGetProperty("link", out var l) ? l.GetString() : null;
            var date = body.GetProperty("date").GetString() ?? DateTime.Today.ToString("yyyy-MM-dd");
            var weekNumber = body.TryGetProperty("weekNumber", out var w) && w.ValueKind != JsonValueKind.Null ? w.GetInt32() : (int?)null;
            var taskId = body.TryGetProperty("taskId", out var t) && t.ValueKind != JsonValueKind.Null ? t.GetInt32() : (int?)null;
            var type = body.TryGetProperty("type", out var tp) ? tp.GetString() ?? "expense" : "expense";
            var expense = repo.Update(projectId, id, categoryId, amount, description, link, date, weekNumber, taskId, type);
            activityRepo.Add(projectId, "finance", "updated", "Finanz-Eintrag aktualisiert", description, request.HttpContext.User?.Identity?.Name);
            return Results.Ok(expense);
        });

        // GET /api/milestones
        app.MapGet("/api/milestones", (HttpRequest req, HttpContext ctx, IMilestoneRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(req, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            return Results.Ok(repo.GetAll(projectId));
        });

        // GET /api/milestones/{id}
        app.MapGet("/api/milestones/{id}", (int id, HttpRequest request, HttpContext ctx, IMilestoneRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            try { return Results.Ok(repo.GetById(projectId, id)); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        // POST /api/milestones
        app.MapPost("/api/milestones", async (HttpRequest request, HttpContext ctx, IMilestoneRepository repo, IActivityRepository activityRepo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            var name = body.GetProperty("name").GetString() ?? "";
            var description = body.TryGetProperty("description", out var d) ? d.GetString() : null;
            var snapshot = body.GetProperty("snapshot").GetString() ?? "{}";
            var milestone = repo.Create(projectId, name, description, snapshot);
            activityRepo.Add(projectId, "milestone", "created", "Meilenstein erstellt", name, request.HttpContext.User?.Identity?.Name);
            return Results.Ok(milestone);
        });

        // DELETE /api/milestones/{id}
        app.MapDelete("/api/milestones/{id}", (int id, HttpRequest request, HttpContext ctx, IMilestoneRepository repo, IActivityRepository activityRepo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            repo.Delete(projectId, id);
            activityRepo.Add(projectId, "milestone", "deleted", "Meilenstein gelöscht", $"ID {id}", request.HttpContext.User?.Identity?.Name);
            return Results.Ok(new { deleted = true });
        });

        return app;
    }
}
