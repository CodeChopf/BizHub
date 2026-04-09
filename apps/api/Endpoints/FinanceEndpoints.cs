using System.Text.Json;
using AuraPrintsApi.Repositories;

namespace AuraPrintsApi.Endpoints;

public static class FinanceEndpoints
{
    public static WebApplication MapFinanceEndpoints(this WebApplication app)
    {
        // GET /api/finance
        app.MapGet("/api/finance", (HttpRequest req, IExpenseRepository repo) =>
            Results.Ok(repo.GetAll(ApiHelpers.GetProjectId(req))));

        // POST /api/expenses
        app.MapPost("/api/expenses", async (HttpRequest request, IExpenseRepository repo, IActivityRepository activityRepo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            var categoryId = body.GetProperty("categoryId").GetInt32();
            var amount = body.GetProperty("amount").GetDecimal();
            var description = body.GetProperty("description").GetString() ?? "";
            var link = body.TryGetProperty("link", out var l) ? l.GetString() : null;
            var date = body.GetProperty("date").GetString() ?? DateTime.Today.ToString("yyyy-MM-dd");
            var weekNumber = body.TryGetProperty("weekNumber", out var w) && w.ValueKind != JsonValueKind.Null ? w.GetInt32() : (int?)null;
            var taskId = body.TryGetProperty("taskId", out var t) && t.ValueKind != JsonValueKind.Null ? t.GetInt32() : (int?)null;
            var type = body.TryGetProperty("type", out var tp) ? tp.GetString() ?? "expense" : "expense";
            var projectId = ApiHelpers.GetProjectId(request);
            var expense = repo.Add(projectId, categoryId, amount, description, link, date, weekNumber, taskId, type);
            activityRepo.Add(projectId, "finance", "created",
                type == "income" ? "Einnahme erfasst" : "Ausgabe erfasst",
                description, request.HttpContext.User?.Identity?.Name);
            return Results.Ok(expense);
        });

        // DELETE /api/expenses/{id}
        app.MapDelete("/api/expenses/{id}", (int id, HttpRequest request, IExpenseRepository repo, IActivityRepository activityRepo) =>
        {
            repo.Delete(id);
            var projectId = ApiHelpers.GetProjectId(request);
            activityRepo.Add(projectId, "finance", "deleted", "Finanz-Eintrag gelöscht", $"ID {id}", request.HttpContext.User?.Identity?.Name);
            return Results.Ok(new { deleted = true });
        });

        // GET /api/categories
        app.MapGet("/api/categories", (HttpRequest req, ICategoryRepository repo) =>
            Results.Ok(repo.GetAll(ApiHelpers.GetProjectId(req))));

        // POST /api/categories
        app.MapPost("/api/categories", async (HttpRequest request, ICategoryRepository repo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            var name = body.GetProperty("name").GetString() ?? "";
            var color = body.TryGetProperty("color", out var c) ? c.GetString() ?? "#4f8ef7" : "#4f8ef7";
            return Results.Ok(repo.Add(ApiHelpers.GetProjectId(request), name, color));
        });

        // PUT /api/categories/{id}
        app.MapPut("/api/categories/{id}", async (int id, HttpRequest request, ICategoryRepository repo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            var name = body.GetProperty("name").GetString() ?? "";
            var color = body.TryGetProperty("color", out var c) ? c.GetString() ?? "#4f8ef7" : "#4f8ef7";
            return Results.Ok(repo.Update(id, name, color));
        });

        // DELETE /api/categories/{id}
        app.MapDelete("/api/categories/{id}", (int id, ICategoryRepository repo) =>
        {
            repo.Delete(id);
            return Results.Ok(new { deleted = true });
        });

        // GET /api/expenses/{id}/attachments
        app.MapGet("/api/expenses/{id}/attachments", (int id, IAttachmentRepository repo) =>
            Results.Ok(repo.GetByExpenseId(id)));

        // POST /api/expenses/{id}/attachments
        app.MapPost("/api/expenses/{id}/attachments", async (int id, HttpRequest request, IAttachmentRepository repo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            var fileName = body.GetProperty("fileName").GetString() ?? "beleg";
            var mimeType = body.GetProperty("mimeType").GetString() ?? "image/jpeg";
            var data = body.GetProperty("data").GetString() ?? "";
            if (string.IsNullOrEmpty(data)) return Results.BadRequest();
            var attachment = repo.Add(id, fileName, mimeType, data);
            return Results.Ok(attachment);
        });

        // DELETE /api/attachments/{id}
        app.MapDelete("/api/attachments/{id}", (int id, IAttachmentRepository repo) =>
        {
            repo.Delete(id);
            return Results.Ok(new { deleted = true });
        });

        // PUT /api/expenses/{id}
        app.MapPut("/api/expenses/{id}", async (int id, HttpRequest request, IExpenseRepository repo, IActivityRepository activityRepo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            var categoryId = body.GetProperty("categoryId").GetInt32();
            var amount = body.GetProperty("amount").GetDecimal();
            var description = body.GetProperty("description").GetString() ?? "";
            var link = body.TryGetProperty("link", out var l) ? l.GetString() : null;
            var date = body.GetProperty("date").GetString() ?? DateTime.Today.ToString("yyyy-MM-dd");
            var weekNumber = body.TryGetProperty("weekNumber", out var w) && w.ValueKind != JsonValueKind.Null ? w.GetInt32() : (int?)null;
            var taskId = body.TryGetProperty("taskId", out var t) && t.ValueKind != JsonValueKind.Null ? t.GetInt32() : (int?)null;
            var type = body.TryGetProperty("type", out var tp) ? tp.GetString() ?? "expense" : "expense";
            var expense = repo.Update(id, categoryId, amount, description, link, date, weekNumber, taskId, type);
            var projectId = ApiHelpers.GetProjectId(request);
            activityRepo.Add(projectId, "finance", "updated", "Finanz-Eintrag aktualisiert", description, request.HttpContext.User?.Identity?.Name);
            return Results.Ok(expense);
        });

        // GET /api/milestones
        app.MapGet("/api/milestones", (HttpRequest req, IMilestoneRepository repo) =>
            Results.Ok(repo.GetAll(ApiHelpers.GetProjectId(req))));

        // GET /api/milestones/{id}
        app.MapGet("/api/milestones/{id}", (int id, IMilestoneRepository repo) =>
        {
            try { return Results.Ok(repo.GetById(id)); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        // POST /api/milestones
        app.MapPost("/api/milestones", async (HttpRequest request, IMilestoneRepository repo, IActivityRepository activityRepo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            var name = body.GetProperty("name").GetString() ?? "";
            var description = body.TryGetProperty("description", out var d) ? d.GetString() : null;
            var snapshot = body.GetProperty("snapshot").GetString() ?? "{}";
            var projectId = ApiHelpers.GetProjectId(request);
            var milestone = repo.Create(projectId, name, description, snapshot);
            activityRepo.Add(projectId, "milestone", "created", "Meilenstein erstellt", name, request.HttpContext.User?.Identity?.Name);
            return Results.Ok(milestone);
        });

        // DELETE /api/milestones/{id}
        app.MapDelete("/api/milestones/{id}", (int id, HttpRequest request, IMilestoneRepository repo, IActivityRepository activityRepo) =>
        {
            repo.Delete(id);
            var projectId = ApiHelpers.GetProjectId(request);
            activityRepo.Add(projectId, "milestone", "deleted", "Meilenstein gelöscht", $"ID {id}", request.HttpContext.User?.Identity?.Name);
            return Results.Ok(new { deleted = true });
        });

        return app;
    }
}
