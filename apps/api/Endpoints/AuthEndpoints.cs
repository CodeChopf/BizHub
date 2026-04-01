using System.Security.Claims;
using System.Text.Json;
using AuraPrintsApi.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using BC = BCrypt.Net.BCrypt;

namespace AuraPrintsApi.Endpoints;

public static class AuthEndpoints
{
    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        // POST /api/auth/login
        app.MapPost("/api/auth/login", async (HttpRequest request, HttpContext ctx, IUserRepository userRepo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            var username = body.TryGetProperty("username", out var u) ? u.GetString() : null;
            var password = body.TryGetProperty("password", out var p) ? p.GetString() : null;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return Results.BadRequest(new { error = "Benutzername und Passwort sind Pflicht." });

            var user = userRepo.GetByUsername(username);
            if (user == null || user.PasswordHash == null || !BC.Verify(password, user.PasswordHash))
                return Results.Json(new { error = "Falscher Benutzername oder Passwort." }, statusCode: 401);

            var claims = new List<Claim> { new Claim(ClaimTypes.Name, user.Username) };
            if (user.IsAdmin) claims.Add(new Claim(ClaimTypes.Role, "admin"));
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            return Results.Ok(new { ok = true, username = user.Username, isAdmin = user.IsAdmin });
        }).AllowAnonymous().RequireRateLimiting("login");

        // POST /api/auth/logout
        app.MapPost("/api/auth/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok(new { ok = true });
        });

        // GET /api/auth/me
        app.MapGet("/api/auth/me", (HttpContext ctx) =>
            ctx.User.Identity?.IsAuthenticated == true
                ? Results.Ok(new {
                    ok = true,
                    username = ctx.User.Identity.Name,
                    isAdmin = ctx.User.IsInRole("admin")
                  })
                : Results.Unauthorized()
        ).AllowAnonymous();

        // POST /api/auth/register — Account erstellen via Platform-Invite-Token
        app.MapPost("/api/auth/register", async (HttpRequest request, IUserRepository userRepo, IInviteRepository inviteRepo) =>
        {
            var body     = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            var token    = body.TryGetProperty("token",    out var t) ? t.GetString() : null;
            var username = body.TryGetProperty("username", out var u) ? u.GetString() : null;
            var password = body.TryGetProperty("password", out var p) ? p.GetString() : null;
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return Results.BadRequest(new { error = "Token, Benutzername und Passwort sind Pflicht." });
            var invite = inviteRepo.GetByToken(token);
            if (invite == null || invite.Type != "platform")
                return Results.BadRequest(new { error = "Ungültiger Einladungslink." });
            if (invite.UsedAt != null)
                return Results.BadRequest(new { error = "Einladungslink wurde bereits verwendet." });
            if (DateTime.TryParse(invite.ExpiresAt, out var exp) && exp < DateTime.UtcNow)
                return Results.BadRequest(new { error = "Einladungslink ist abgelaufen." });
            if (userRepo.GetByUsername(username) != null)
                return Results.BadRequest(new { error = "Benutzername bereits vergeben." });
            userRepo.CreateWithHash(username, BC.HashPassword(password), isAdmin: false);
            inviteRepo.MarkUsed(token);
            return Results.Ok(new { ok = true });
        }).AllowAnonymous();

        return app;
    }
}
