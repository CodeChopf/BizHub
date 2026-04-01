using System.Text.Json;
using AuraPrintsApi.Repositories;

namespace AuraPrintsApi.Endpoints;

public static class CalendarEndpoints
{
    public static WebApplication MapCalendarEndpoints(this WebApplication app)
    {
        // GET /api/calendar
        app.MapGet("/api/calendar", (HttpRequest req, ICalendarRepository repo) =>
            Results.Ok(repo.GetAll(ApiHelpers.GetProjectId(req))));

        // POST /api/calendar
        app.MapPost("/api/calendar", async (HttpRequest request, ICalendarRepository repo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
            var title       = body.GetProperty("title").GetString() ?? "";
            var date        = body.GetProperty("date").GetString() ?? "";
            var endDate     = body.TryGetProperty("endDate", out var ed) && ed.ValueKind != JsonValueKind.Null ? ed.GetString() : null;
            var time        = body.TryGetProperty("time", out var t) && t.ValueKind != JsonValueKind.Null ? t.GetString() : null;
            var description = body.TryGetProperty("description", out var d) && d.ValueKind != JsonValueKind.Null ? d.GetString() : null;
            var color       = body.TryGetProperty("color", out var c) && c.ValueKind != JsonValueKind.Null ? c.GetString() ?? "#4f8ef7" : "#4f8ef7";
            var type        = body.TryGetProperty("type", out var ty) && ty.ValueKind != JsonValueKind.Null ? ty.GetString() ?? "event" : "event";
            return Results.Ok(repo.Add(ApiHelpers.GetProjectId(request), title, date, endDate, time, description, color, type));
        });

        // PUT /api/calendar/{id}
        app.MapPut("/api/calendar/{id}", async (int id, HttpRequest request, ICalendarRepository repo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
            var title       = body.GetProperty("title").GetString() ?? "";
            var date        = body.GetProperty("date").GetString() ?? "";
            var endDate     = body.TryGetProperty("endDate", out var ed) && ed.ValueKind != JsonValueKind.Null ? ed.GetString() : null;
            var time        = body.TryGetProperty("time", out var t) && t.ValueKind != JsonValueKind.Null ? t.GetString() : null;
            var description = body.TryGetProperty("description", out var d) && d.ValueKind != JsonValueKind.Null ? d.GetString() : null;
            var color       = body.TryGetProperty("color", out var c) && c.ValueKind != JsonValueKind.Null ? c.GetString() ?? "#4f8ef7" : "#4f8ef7";
            var type        = body.TryGetProperty("type", out var ty) && ty.ValueKind != JsonValueKind.Null ? ty.GetString() ?? "event" : "event";
            repo.Update(id, title, date, endDate, time, description, color, type);
            return Results.Ok(new { updated = true });
        });

        // DELETE /api/calendar/{id}
        app.MapDelete("/api/calendar/{id}", (int id, ICalendarRepository repo) =>
        {
            repo.Delete(id);
            return Results.Ok(new { deleted = true });
        });

        return app;
    }
}
