using System.Text.Json;
using AuraPrintsApi.Repositories;

namespace AuraPrintsApi.Endpoints;

public static class UserEndpoints
{
    public static WebApplication MapUserEndpoints(this WebApplication app)
    {
        // GET /api/users
        app.MapGet("/api/users", (HttpContext ctx, IUserRepository userRepo) =>
        {
            if (!ctx.User.IsInRole("admin")) return Results.Forbid();
            return Results.Ok(userRepo.GetAll());
        });

        // POST /api/users
        app.MapPost("/api/users", async (HttpRequest request, HttpContext ctx, IUserRepository userRepo) =>
        {
            if (!ctx.User.IsInRole("admin")) return Results.Forbid();
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            var username = body.TryGetProperty("username", out var u) ? u.GetString() : null;
            var password = body.TryGetProperty("password", out var p) ? p.GetString() : null;
            var isAdmin  = body.TryGetProperty("isAdmin", out var a) && a.GetBoolean();
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return Results.BadRequest(new { error = "Benutzername und Passwort sind Pflicht." });
            if (userRepo.GetByUsername(username) != null)
                return Results.BadRequest(new { error = "Benutzername bereits vergeben." });
            return Results.Ok(userRepo.Create(username, password, isAdmin));
        });

        // DELETE /api/users/{username}
        app.MapDelete("/api/users/{username}", (string username, HttpContext ctx, IUserRepository userRepo) =>
        {
            if (!ctx.User.IsInRole("admin")) return Results.Forbid();
            if (ctx.User.Identity?.Name == username)
                return Results.BadRequest(new { error = "Du kannst dich nicht selbst löschen." });
            userRepo.Delete(username);
            return Results.Ok(new { deleted = true });
        });

        // PUT /api/users/{username}/password
        app.MapPut("/api/users/{username}/password", async (string username, HttpRequest request, HttpContext ctx, IUserRepository userRepo) =>
        {
            if (!ctx.User.IsInRole("admin")) return Results.Forbid();
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            var password = body.TryGetProperty("password", out var p) ? p.GetString() : null;
            if (string.IsNullOrEmpty(password))
                return Results.BadRequest(new { error = "Passwort darf nicht leer sein." });
            userRepo.ChangePassword(username, password);
            return Results.Ok(new { updated = true });
        });

        return app;
    }
}
