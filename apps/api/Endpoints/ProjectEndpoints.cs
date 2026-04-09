using System.Text.Json;
using AuraPrintsApi.Repositories;

namespace AuraPrintsApi.Endpoints;

public static class ProjectEndpoints
{
    public static WebApplication MapProjectEndpoints(this WebApplication app)
    {
        // GET /api/projects
        app.MapGet("/api/projects", (HttpContext ctx, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var user = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
            if (user == null) return Results.Unauthorized();
            return Results.Ok(projectRepo.GetForUser(user.Id));
        });

        // POST /api/projects
        app.MapPost("/api/projects", async (HttpRequest request, HttpContext ctx, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var user = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
            if (user == null) return Results.Unauthorized();
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
            var name        = body.GetProperty("name").GetString() ?? "";
            var description = body.TryGetProperty("description", out var d) ? d.GetString() : null;
            var startDate   = body.TryGetProperty("startDate", out var sd) && sd.ValueKind != JsonValueKind.Null ? sd.GetString() : null;
            var currency    = body.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "CHF" : "CHF";
            return Results.Ok(projectRepo.Create(name, description, startDate, currency, user.Id));
        });

        // GET /api/projects/{id}
        app.MapGet("/api/projects/{id}", (int id, HttpContext ctx, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var user = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
            if (user == null) return Results.Unauthorized();
            if (!projectRepo.IsMember(id, user.Id)) return Results.Forbid();
            var project = projectRepo.GetById(id);
            return project == null ? Results.NotFound() : Results.Ok(project);
        });

        // PUT /api/projects/{id}
        app.MapPut("/api/projects/{id}", async (int id, HttpRequest request, HttpContext ctx, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var user = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
            if (user == null) return Results.Unauthorized();
            var role = projectRepo.GetRole(id, user.Id);
            if (role != "admin") return Results.Forbid();
            var body         = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
            var name         = body.GetProperty("name").GetString() ?? "";
            var description  = body.TryGetProperty("description", out var d) ? d.GetString() : null;
            var startDate    = body.TryGetProperty("startDate", out var sd) && sd.ValueKind != JsonValueKind.Null ? sd.GetString() : null;
            var currency     = body.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "CHF" : "CHF";
            var projectImage = body.TryGetProperty("projectImage", out var pi) && pi.ValueKind != JsonValueKind.Null ? pi.GetString() : null;
            var visibleTabs  = body.TryGetProperty("visibleTabs", out var vt) && vt.ValueKind != JsonValueKind.Null ? vt.GetString() : null;
            projectRepo.Update(id, name, description, startDate, currency, projectImage, visibleTabs);
            return Results.Ok(projectRepo.GetById(id));
        });

        // GET /api/projects/{id}/members
        app.MapGet("/api/projects/{id}/members", (int id, HttpContext ctx, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var user = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
            if (user == null) return Results.Unauthorized();
            if (!projectRepo.IsMember(id, user.Id)) return Results.Forbid();
            return Results.Ok(projectRepo.GetMembers(id));
        });

        // POST /api/projects/{id}/members
        app.MapPost("/api/projects/{id}/members", async (int id, HttpRequest request, HttpContext ctx, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var currentUser = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
            if (currentUser == null) return Results.Unauthorized();
            var role = projectRepo.GetRole(id, currentUser.Id);
            if (role != "admin") return Results.Forbid();
            var body     = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
            var username = body.GetProperty("username").GetString() ?? "";
            var newRole  = body.TryGetProperty("role", out var r) ? r.GetString() ?? "member" : "member";
            var target   = userRepo.GetByUsername(username);
            if (target == null) return Results.BadRequest(new { error = "Benutzer nicht gefunden." });
            projectRepo.AddMember(id, target.Id, newRole);
            return Results.Ok(new { added = true });
        });

        // DELETE /api/projects/{id}/members/{userId}
        app.MapDelete("/api/projects/{id}/members/{userId}", (int id, int userId, HttpContext ctx, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var currentUser = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
            if (currentUser == null) return Results.Unauthorized();
            var role = projectRepo.GetRole(id, currentUser.Id);
            if (role != "admin") return Results.Forbid();
            if (userId == currentUser.Id) return Results.BadRequest(new { error = "Du kannst dich nicht selbst entfernen." });
            projectRepo.RemoveMember(id, userId);
            return Results.Ok(new { removed = true });
        });

        // POST /api/projects/{id}/invites
        app.MapPost("/api/projects/{id}/invites", async (int id, HttpRequest request, HttpContext ctx, IUserRepository userRepo, IProjectRepository projectRepo, IInviteRepository inviteRepo) =>
        {
            var currentUser = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
            if (currentUser == null) return Results.Unauthorized();
            var role = projectRepo.GetRole(id, currentUser.Id);
            if (role != "admin") return Results.Forbid();
            var body       = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
            var inviteRole = body.TryGetProperty("role", out var r) ? r.GetString() ?? "member" : "member";
            var hours      = body.TryGetProperty("hoursValid", out var h) ? h.GetInt32() : 48;
            var invite     = inviteRepo.Create("project", id, inviteRole, currentUser.Id, hours);
            return Results.Ok(invite);
        });

        // POST /api/invites/{token}/accept
        app.MapPost("/api/invites/{token}/accept", async (string token, HttpRequest request, HttpContext ctx, IUserRepository userRepo, IProjectRepository projectRepo, IInviteRepository inviteRepo) =>
        {
            var invite = inviteRepo.GetByToken(token);
            if (invite == null) return Results.NotFound(new { error = "Einladung nicht gefunden." });
            if (invite.UsedAt != null) return Results.BadRequest(new { error = "Einladung wurde bereits verwendet." });
            if (DateTime.TryParse(invite.ExpiresAt, out var expires) && expires < DateTime.UtcNow)
                return Results.BadRequest(new { error = "Einladung ist abgelaufen." });

            var currentUser = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
            if (currentUser == null) return Results.Unauthorized();

            if (invite.ProjectId.HasValue)
                projectRepo.AddMember(invite.ProjectId.Value, currentUser.Id, invite.Role);

            inviteRepo.MarkUsed(token);
            return Results.Ok(new { accepted = true, projectId = invite.ProjectId });
        });

        // POST /api/projects/{id}/leave
        app.MapPost("/api/projects/{id}/leave", (int id, HttpContext ctx, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var user = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
            if (user == null) return Results.Unauthorized();
            if (!projectRepo.IsMember(id, user.Id)) return Results.BadRequest(new { error = "Nicht Mitglied." });

            var members = projectRepo.GetMembers(id);
            if (members.Count == 1)
                return Results.BadRequest(new { error = "Du bist das letzte Mitglied. Lösche das Projekt, um es zu entfernen." });

            var role = projectRepo.GetRole(id, user.Id);
            if (role == "admin")
            {
                var next = members.FirstOrDefault(m => m.UserId != user.Id);
                if (next != null) projectRepo.AddMember(id, next.UserId, "admin");
            }

            projectRepo.RemoveMember(id, user.Id);
            return Results.Ok(new { left = true });
        });

        // DELETE /api/projects/{id}
        app.MapDelete("/api/projects/{id}", (int id, HttpContext ctx, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var user = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
            if (user == null) return Results.Unauthorized();
            var role = projectRepo.GetRole(id, user.Id);
            if (role != "admin") return Results.Forbid();
            projectRepo.DeleteProject(id);
            return Results.Ok(new { deleted = true });
        });

        // GET /api/invites/{token} — Einladung prüfen ohne zu verbrauchen
        app.MapGet("/api/invites/{token}", (string token, IInviteRepository inviteRepo) =>
        {
            var invite = inviteRepo.GetByToken(token);
            if (invite == null) return Results.NotFound(new { error = "Einladung nicht gefunden." });
            if (invite.UsedAt != null) return Results.BadRequest(new { error = "Einladung wurde bereits verwendet." });
            if (DateTime.TryParse(invite.ExpiresAt, out var exp) && exp < DateTime.UtcNow)
                return Results.BadRequest(new { error = "Einladung ist abgelaufen." });
            return Results.Ok(new { type = invite.Type, projectName = invite.ProjectName });
        }).AllowAnonymous();

        // POST /api/platform/invites
        app.MapPost("/api/platform/invites", async (HttpRequest request, HttpContext ctx, IUserRepository userRepo, IInviteRepository inviteRepo) =>
        {
            if (!ctx.User.IsInRole("admin")) return Results.Forbid();
            var user = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
            if (user == null) return Results.Unauthorized();
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
            var hours = body.TryGetProperty("hoursValid", out var h) && h.TryGetInt32(out var hv) ? hv : 48;
            var invite = inviteRepo.Create("platform", null, "user", user.Id, hours);
            return Results.Ok(invite);
        });

        return app;
    }
}
