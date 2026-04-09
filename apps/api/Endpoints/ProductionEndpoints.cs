using System.Text.Json;
using AuraPrintsApi.Repositories;

namespace AuraPrintsApi.Endpoints;

public static class ProductionEndpoints
{
    public static WebApplication MapProductionEndpoints(this WebApplication app)
    {
        // GET /api/production
        app.MapGet("/api/production", (HttpRequest req, IProductionRepository repo) =>
            Results.Ok(repo.GetAll(ApiHelpers.GetProjectId(req))));

        // POST /api/production
        app.MapPost("/api/production", async (HttpRequest request, IProductionRepository repo, IActivityRepository activityRepo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
            var productId   = body.GetProperty("productId").GetInt32();
            var variationId = body.TryGetProperty("variationId", out var vid) && vid.ValueKind != JsonValueKind.Null ? vid.GetInt32() : (int?)null;
            var quantity    = body.TryGetProperty("quantity", out var qty) ? qty.GetInt32() : 1;
            var note        = body.TryGetProperty("note", out var n) && n.ValueKind != JsonValueKind.Null ? n.GetString() : null;
            var projectId = ApiHelpers.GetProjectId(request);
            var item = repo.Add(projectId, productId, variationId, quantity, note);
            activityRepo.Add(projectId, "production", "created", "Produkt zur Produktion hinzugefügt",
                $"{item.ProductName} · Menge {item.Quantity}", request.HttpContext.User?.Identity?.Name);
            return Results.Ok(item);
        });

        // PATCH /api/production/{id}/done
        app.MapMethods("/api/production/{id}/done", ["PATCH"], async (int id, HttpRequest request, IProductionRepository repo, IActivityRepository activityRepo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
            var done = body.GetProperty("done").GetBoolean();
            repo.SetDone(id, done);
            var projectId = ApiHelpers.GetProjectId(request);
            activityRepo.Add(projectId, "production", "updated",
                done ? "Produktionspunkt erledigt" : "Produktionspunkt wieder offen",
                $"ID {id}", request.HttpContext.User?.Identity?.Name);
            return Results.Ok(new { updated = true });
        });

        // PUT /api/production/{id}
        app.MapPut("/api/production/{id}", async (int id, HttpRequest request, IProductionRepository repo, IActivityRepository activityRepo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
            var quantity = body.GetProperty("quantity").GetInt32();
            var note     = body.TryGetProperty("note", out var n) && n.ValueKind != JsonValueKind.Null ? n.GetString() : null;
            repo.UpdateItem(id, quantity, note);
            var projectId = ApiHelpers.GetProjectId(request);
            activityRepo.Add(projectId, "production", "updated", "Produktionseintrag aktualisiert",
                $"ID {id} · Menge {quantity}", request.HttpContext.User?.Identity?.Name);
            return Results.Ok(new { updated = true });
        });

        // DELETE /api/production/done (muss vor /{id} stehen!)
        app.MapDelete("/api/production/done", (HttpRequest request, IProductionRepository repo, IActivityRepository activityRepo) =>
        {
            repo.DeleteAllDone();
            var projectId = ApiHelpers.GetProjectId(request);
            activityRepo.Add(projectId, "production", "deleted", "Erledigte Produktionseinträge gelöscht",
                null, request.HttpContext.User?.Identity?.Name);
            return Results.Ok(new { deleted = true });
        });

        // DELETE /api/production/{id}
        app.MapDelete("/api/production/{id}", (int id, HttpRequest request, IProductionRepository repo, IActivityRepository activityRepo) =>
        {
            repo.Delete(id);
            var projectId = ApiHelpers.GetProjectId(request);
            activityRepo.Add(projectId, "production", "deleted", "Produktionseintrag gelöscht",
                $"ID {id}", request.HttpContext.User?.Identity?.Name);
            return Results.Ok(new { deleted = true });
        });

        return app;
    }
}
