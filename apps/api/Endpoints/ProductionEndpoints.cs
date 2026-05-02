using System.Text.Json;
using AuraPrintsApi.Repositories;

namespace AuraPrintsApi.Endpoints;

public static class ProductionEndpoints
{
    public static WebApplication MapProductionEndpoints(this WebApplication app)
    {
        // GET /api/production
        app.MapGet("/api/production", (HttpRequest req, HttpContext ctx, IProductionRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(req, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            return Results.Ok(repo.GetAll(projectId));
        });

        // POST /api/production
        app.MapPost("/api/production", async (HttpRequest request, HttpContext ctx, IProductionRepository repo, IActivityRepository activityRepo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
            var productId   = body.GetProperty("productId").GetInt32();
            var variationId = body.TryGetProperty("variationId", out var vid) && vid.ValueKind != JsonValueKind.Null ? vid.GetInt32() : (int?)null;
            var quantity    = body.TryGetProperty("quantity", out var qty) ? qty.GetInt32() : 1;
            var note        = body.TryGetProperty("note", out var n) && n.ValueKind != JsonValueKind.Null ? n.GetString() : null;
            var item = repo.Add(projectId, productId, variationId, quantity, note);
            activityRepo.Add(projectId, "production", "created", "Produkt zur Produktion hinzugefügt",
                $"{item.ProductName} · Menge {item.Quantity}", request.HttpContext.User?.Identity?.Name);
            return Results.Ok(item);
        });

        // PATCH /api/production/{id}/done
        app.MapMethods("/api/production/{id}/done", ["PATCH"], async (int id, HttpRequest request, HttpContext ctx, IProductionRepository repo, IActivityRepository activityRepo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
            var done = body.GetProperty("done").GetBoolean();
            repo.SetDone(projectId, id, done);
            activityRepo.Add(projectId, "production", "updated",
                done ? "Produktionspunkt erledigt" : "Produktionspunkt wieder offen",
                $"ID {id}", request.HttpContext.User?.Identity?.Name);
            return Results.Ok(new { updated = true });
        });

        // PUT /api/production/{id}
        app.MapPut("/api/production/{id}", async (int id, HttpRequest request, HttpContext ctx, IProductionRepository repo, IActivityRepository activityRepo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
            var quantity = body.GetProperty("quantity").GetInt32();
            var note     = body.TryGetProperty("note", out var n) && n.ValueKind != JsonValueKind.Null ? n.GetString() : null;
            repo.UpdateItem(projectId, id, quantity, note);
            activityRepo.Add(projectId, "production", "updated", "Produktionseintrag aktualisiert",
                $"ID {id} · Menge {quantity}", request.HttpContext.User?.Identity?.Name);
            return Results.Ok(new { updated = true });
        });

        // DELETE /api/production/done (muss vor /{id} stehen!)
        app.MapDelete("/api/production/done", (HttpRequest request, HttpContext ctx, IProductionRepository repo, IActivityRepository activityRepo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            repo.DeleteAllDone(projectId);
            activityRepo.Add(projectId, "production", "deleted", "Erledigte Produktionseinträge gelöscht",
                null, request.HttpContext.User?.Identity?.Name);
            return Results.Ok(new { deleted = true });
        });

        // DELETE /api/production/{id}
        app.MapDelete("/api/production/{id}", (int id, HttpRequest request, HttpContext ctx, IProductionRepository repo, IActivityRepository activityRepo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var auth = ApiHelpers.EnsureProjectMember(request, ctx, userRepo, projectRepo, out var projectId);
            if (auth != null) return auth;
            repo.Delete(projectId, id);
            activityRepo.Add(projectId, "production", "deleted", "Produktionseintrag gelöscht",
                $"ID {id}", request.HttpContext.User?.Identity?.Name);
            return Results.Ok(new { deleted = true });
        });

        return app;
    }
}
