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
        app.MapPost("/api/production", async (HttpRequest request, IProductionRepository repo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
            var productId   = body.GetProperty("productId").GetInt32();
            var variationId = body.TryGetProperty("variationId", out var vid) && vid.ValueKind != JsonValueKind.Null ? vid.GetInt32() : (int?)null;
            var quantity    = body.TryGetProperty("quantity", out var qty) ? qty.GetInt32() : 1;
            var note        = body.TryGetProperty("note", out var n) && n.ValueKind != JsonValueKind.Null ? n.GetString() : null;
            return Results.Ok(repo.Add(ApiHelpers.GetProjectId(request), productId, variationId, quantity, note));
        });

        // PATCH /api/production/{id}/done
        app.MapMethods("/api/production/{id}/done", ["PATCH"], async (int id, HttpRequest request, IProductionRepository repo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
            var done = body.GetProperty("done").GetBoolean();
            repo.SetDone(id, done);
            return Results.Ok(new { updated = true });
        });

        // PUT /api/production/{id}
        app.MapPut("/api/production/{id}", async (int id, HttpRequest request, IProductionRepository repo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
            var quantity = body.GetProperty("quantity").GetInt32();
            var note     = body.TryGetProperty("note", out var n) && n.ValueKind != JsonValueKind.Null ? n.GetString() : null;
            repo.UpdateItem(id, quantity, note);
            return Results.Ok(new { updated = true });
        });

        // DELETE /api/production/done (muss vor /{id} stehen!)
        app.MapDelete("/api/production/done", (IProductionRepository repo) =>
        {
            repo.DeleteAllDone();
            return Results.Ok(new { deleted = true });
        });

        // DELETE /api/production/{id}
        app.MapDelete("/api/production/{id}", (int id, IProductionRepository repo) =>
        {
            repo.Delete(id);
            return Results.Ok(new { deleted = true });
        });

        return app;
    }
}
