using System.Security.Claims;
using System.Text.Json;
using AuraPrintsApi.Data;
using AuraPrintsApi.Models;
using AuraPrintsApi.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using BC = BCrypt.Net.BCrypt;

var builder = WebApplication.CreateBuilder(args);

var dataDir = Environment.GetEnvironmentVariable("BIZHUB_DATA_DIR")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BizHub", "Data");
Directory.CreateDirectory(dataDir);
var dbFile = Path.Combine(dataDir, "auraprints.db");
var dbContext = new DatabaseContext(dbFile);

builder.Services.AddSingleton(dbContext);
builder.Services.AddSingleton<IRoadmapRepository, RoadmapRepository>();
builder.Services.AddSingleton<IProductRepository, ProductRepository>();
builder.Services.AddSingleton<IStateRepository, StateRepository>();
builder.Services.AddSingleton<ICategoryRepository, CategoryRepository>();
builder.Services.AddSingleton<IExpenseRepository, ExpenseRepository>();
builder.Services.AddSingleton<IAdminRepository, AdminRepository>();
builder.Services.AddSingleton<IAttachmentRepository, AttachmentRepository>();
builder.Services.AddSingleton<IMilestoneRepository, MilestoneRepository>();
builder.Services.AddSingleton<ISettingsRepository, SettingsRepository>();
builder.Services.AddSingleton<IProductCatalogRepository, ProductCatalogRepository>();
builder.Services.AddSingleton<IProductionRepository, ProductionRepository>();
builder.Services.AddSingleton<ICalendarRepository, CalendarRepository>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IProjectRepository, ProjectRepository>();
builder.Services.AddSingleton<IInviteRepository, InviteRepository>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o => {
        o.Cookie.Name = "bizhub_session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.ExpireTimeSpan = TimeSpan.FromHours(24);
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    });

builder.Services.AddAuthorization(options => {
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddRateLimiter(opt => {
    opt.AddFixedWindowLimiter("login", o => {
        o.PermitLimit = 5;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
    opt.RejectionStatusCode = 429;
});

var app = builder.Build();

var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    WriteIndented = true
};

// Helper: project ID aus Query-Parameter (Standard: 1)
int GetProjectId(HttpRequest req) =>
    req.Query.TryGetValue("projectId", out var p) && int.TryParse(p, out var pid) ? pid : 1;

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

var seeder = new DatabaseSeeder(dbContext);
dbContext.Initialize();
seeder.Seed();

// Bootstrap: migrate single-password or create first admin user
{
    var settingsRepo = app.Services.GetRequiredService<ISettingsRepository>();
    var userRepo = app.Services.GetRequiredService<IUserRepository>();
    if (!userRepo.HasAnyUser())
    {
        var oldHash = settingsRepo.GetPasswordHash();
        if (oldHash != null)
        {
            // Migrate existing password hash to admin user
            userRepo.CreateWithHash("admin", oldHash, isAdmin: true);
            settingsRepo.DeletePasswordHash();
        }
        else
        {
            var envPw = Environment.GetEnvironmentVariable("BIZHUB_PASSWORD");
            if (envPw != null)
                userRepo.Create("admin", envPw, isAdmin: true);
        }
    }
}

// ── AUTH ──

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

// ── BENUTZERVERWALTUNG (nur Admin) ──

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

// GET /api/data
app.MapGet("/api/data", (HttpRequest req, IRoadmapRepository repo) =>
    Results.Ok(repo.GetAll(GetProjectId(req))));

// GET /api/products
app.MapGet("/api/products", (IProductRepository repo) =>
    Results.Ok(repo.GetAll()));

// GET /api/state
app.MapGet("/api/state", (HttpRequest req, IStateRepository repo) =>
    Results.Ok(repo.GetState(GetProjectId(req))));

// POST /api/state
app.MapPost("/api/state", async (HttpRequest request, IStateRepository repo) =>
{
    var state = await JsonSerializer.DeserializeAsync<Dictionary<string, bool>>(request.Body);
    if (state == null) return Results.BadRequest();
    repo.SaveState(GetProjectId(request), state);
    return Results.Ok(new { saved = true });
});

// GET /api/finance
app.MapGet("/api/finance", (HttpRequest req, IExpenseRepository repo) =>
    Results.Ok(repo.GetAll(GetProjectId(req))));

// POST /api/expenses
app.MapPost("/api/expenses", async (HttpRequest request, IExpenseRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
    var categoryId = body.GetProperty("categoryId").GetInt32();
    var amount = body.GetProperty("amount").GetDecimal();
    var description = body.GetProperty("description").GetString() ?? "";
    var link = body.TryGetProperty("link", out var l) ? l.GetString() : null;
    var date = body.GetProperty("date").GetString() ?? DateTime.Today.ToString("yyyy-MM-dd");
    var weekNumber = body.TryGetProperty("weekNumber", out var w) && w.ValueKind != JsonValueKind.Null ? w.GetInt32() : (int?)null;
    var taskId = body.TryGetProperty("taskId", out var t) && t.ValueKind != JsonValueKind.Null ? t.GetInt32() : (int?)null;
    var expense = repo.Add(GetProjectId(request), categoryId, amount, description, link, date, weekNumber, taskId);
    return Results.Ok(expense);
});

// DELETE /api/expenses/{id}
app.MapDelete("/api/expenses/{id}", (int id, IExpenseRepository repo) =>
{
    repo.Delete(id);
    return Results.Ok(new { deleted = true });
});

// GET /api/categories
app.MapGet("/api/categories", (HttpRequest req, ICategoryRepository repo) =>
    Results.Ok(repo.GetAll(GetProjectId(req))));

// POST /api/categories
app.MapPost("/api/categories", async (HttpRequest request, ICategoryRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
    var name = body.GetProperty("name").GetString() ?? "";
    var color = body.GetProperty("color").GetString() ?? "#9699a8";
    var cat = repo.Add(GetProjectId(request), name, color);
    return Results.Ok(cat);
});

// PUT /api/categories/{id}
app.MapPut("/api/categories/{id}", async (int id, HttpRequest request, ICategoryRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
    var name = body.GetProperty("name").GetString() ?? "";
    var color = body.GetProperty("color").GetString() ?? "#9699a8";
    var cat = repo.Update(id, name, color);
    return Results.Ok(cat);
});

// DELETE /api/categories/{id}
app.MapDelete("/api/categories/{id}", (int id, ICategoryRepository repo) =>
{
    repo.Delete(id);
    return Results.Ok(new { deleted = true });
});


// ── ADMIN: WOCHEN ──
app.MapPost("/api/admin/weeks", async (HttpRequest request, IAdminRepository repo) =>
{
    var req = await JsonSerializer.DeserializeAsync<CreateWeekRequest>(request.Body, jsonOptions);
    if (req == null) return Results.BadRequest();
    return Results.Ok(repo.CreateWeek(GetProjectId(request), req));
});

app.MapPut("/api/admin/weeks/{number}", async (int number, HttpRequest request, IAdminRepository repo) =>
{
    var req = await JsonSerializer.DeserializeAsync<UpdateWeekRequest>(request.Body, jsonOptions);
    if (req == null) return Results.BadRequest();
    return Results.Ok(repo.UpdateWeek(GetProjectId(request), number, req));
});

app.MapDelete("/api/admin/weeks/{number}", (int number, HttpRequest request, IAdminRepository repo) =>
{
    repo.DeleteWeek(GetProjectId(request), number);
    return Results.Ok(new { deleted = true });
});

// ── ADMIN: TASKS ──
app.MapPost("/api/admin/tasks", async (HttpRequest request, IAdminRepository repo) =>
{
    var req = await JsonSerializer.DeserializeAsync<CreateTaskRequest>(request.Body, jsonOptions);
    if (req == null) return Results.BadRequest();
    return Results.Ok(repo.CreateTask(GetProjectId(request), req));
});

app.MapPut("/api/admin/tasks/{id}", async (int id, HttpRequest request, IAdminRepository repo) =>
{
    var req = await JsonSerializer.DeserializeAsync<UpdateTaskRequest>(request.Body, jsonOptions);
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
    var req = await JsonSerializer.DeserializeAsync<ReorderTasksRequest>(request.Body, jsonOptions);
    if (req == null) return Results.BadRequest();
    repo.ReorderTasks(GetProjectId(request), number, req);
    return Results.Ok(new { reordered = true });
});

// GET /api/admin/tasks/ids
app.MapGet("/api/admin/tasks/ids", (int weekNumber, HttpRequest request) =>
{
    var projectId = GetProjectId(request);
    using var con = dbContext.CreateConnection();
    con.Open();
    using var cmd = con.CreateCommand();
    cmd.CommandText = @"
        SELECT t.id FROM tasks t
        JOIN weeks w ON w.number = t.week_number AND w.project_id = @pid
        WHERE t.week_number = @w ORDER BY t.sort_order";
    cmd.Parameters.AddWithValue("@pid", projectId);
    cmd.Parameters.AddWithValue("@w", weekNumber);
    var ids = new List<int>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read()) ids.Add(reader.GetInt32(0));
    return Results.Ok(ids);
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
app.MapPut("/api/expenses/{id}", async (int id, HttpRequest request, IExpenseRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
    var categoryId = body.GetProperty("categoryId").GetInt32();
    var amount = body.GetProperty("amount").GetDecimal();
    var description = body.GetProperty("description").GetString() ?? "";
    var link = body.TryGetProperty("link", out var l) ? l.GetString() : null;
    var date = body.GetProperty("date").GetString() ?? DateTime.Today.ToString("yyyy-MM-dd");
    var weekNumber = body.TryGetProperty("weekNumber", out var w) && w.ValueKind != JsonValueKind.Null ? w.GetInt32() : (int?)null;
    var taskId = body.TryGetProperty("taskId", out var t) && t.ValueKind != JsonValueKind.Null ? t.GetInt32() : (int?)null;
    var expense = repo.Update(id, categoryId, amount, description, link, date, weekNumber, taskId);
    return Results.Ok(expense);
});

// GET /api/milestones
app.MapGet("/api/milestones", (HttpRequest req, IMilestoneRepository repo) =>
    Results.Ok(repo.GetAll(GetProjectId(req))));

// GET /api/milestones/{id}
app.MapGet("/api/milestones/{id}", (int id, IMilestoneRepository repo) =>
{
    try { return Results.Ok(repo.GetById(id)); }
    catch (KeyNotFoundException) { return Results.NotFound(); }
});

// POST /api/milestones
app.MapPost("/api/milestones", async (HttpRequest request, IMilestoneRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
    var name = body.GetProperty("name").GetString() ?? "";
    var description = body.TryGetProperty("description", out var d) ? d.GetString() : null;
    var snapshot = body.GetProperty("snapshot").GetString() ?? "{}";
    var milestone = repo.Create(GetProjectId(request), name, description, snapshot);
    return Results.Ok(milestone);
});

// DELETE /api/milestones/{id}
app.MapDelete("/api/milestones/{id}", (int id, IMilestoneRepository repo) =>
{
    repo.Delete(id);
    return Results.Ok(new { deleted = true });
});

// GET /api/settings
app.MapGet("/api/settings", (ISettingsRepository repo) =>
    Results.Ok(repo.GetSettings()));

// POST /api/settings
app.MapPost("/api/settings", async (HttpRequest request, ISettingsRepository repo) =>
{
    var settings = await JsonSerializer.DeserializeAsync<ProjectSettings>(request.Body, jsonOptions);
    if (settings == null) return Results.BadRequest();
    repo.SaveSettings(settings);
    return Results.Ok(repo.GetSettings());
});

// GET /api/export
app.MapGet("/api/export", (HttpRequest request, ISettingsRepository settingsRepo, IRoadmapRepository roadmapRepo, IExpenseRepository expenseRepo, IStateRepository stateRepo, IProductCatalogRepository catalogRepo) =>
{
    var projectId = GetProjectId(request);
    var settings = settingsRepo.GetSettings();
    var roadmap = roadmapRepo.GetAll(projectId);
    var finance = expenseRepo.GetAll(projectId);
    var state = stateRepo.GetState(projectId);
    var catalog = catalogRepo.GetAll(projectId);

    // Katalog manuell aufbauen damit attributeValues als echtes JSON-Objekt exportiert wird
    var catalogJson = new
    {
        categories = catalog.Categories.Select(c => new
        {
            id = c.Id,
            name = c.Name,
            description = c.Description,
            color = c.Color,
            attributes = c.Attributes.Select(a => new
            {
                id = a.Id,
                categoryId = a.CategoryId,
                name = a.Name,
                fieldType = a.FieldType,
                options = a.Options,
                required = a.Required,
                sortOrder = a.SortOrder
            })
        }),
        products = catalog.Products.Select(p =>
        {
            JsonElement attrJson;
            try
            {
                var raw = string.IsNullOrWhiteSpace(p.AttributeValues) ? "{}" : p.AttributeValues;
                attrJson = JsonSerializer.Deserialize<JsonElement>(raw);
            }
            catch
            {
                attrJson = JsonSerializer.Deserialize<JsonElement>("{}");
            }

            return new
            {
                id = p.Id,
                categoryId = p.CategoryId,
                name = p.Name,
                description = p.Description,
                attributeValues = attrJson,
                createdAt = p.CreatedAt,
                variations = p.Variations.Select(v => new
                {
                    id = v.Id,
                    productId = v.ProductId,
                    name = v.Name,
                    sku = v.Sku,
                    price = v.Price,
                    stock = v.Stock,
                    createdAt = v.CreatedAt
                })
            };
        })
    };

    var export = new
    {
        exportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        version = "1.0",
        settings,
        state,
        weeks = roadmap.Weeks,
        finance,
        catalog = catalogJson
    };

    var json = JsonSerializer.Serialize(export, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });
    var bytes = System.Text.Encoding.UTF8.GetBytes(json);
    var fileName = $"{settings.ProjectName.Replace(" ", "_")}_{DateTime.Now:yyyy-MM-dd}.json";
    return Results.File(bytes, "application/json", fileName);
});

// POST /api/import
app.MapPost("/api/import", async (HttpRequest request,
    ISettingsRepository settingsRepo,
    IStateRepository stateRepo,
    IRoadmapRepository roadmapRepo) =>
{
    try
    {
        var projectId = GetProjectId(request);
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);

        // Settings importieren
        if (body.TryGetProperty("settings", out var settingsEl))
        {
            var settings = JsonSerializer.Deserialize<ProjectSettings>(settingsEl.GetRawText(), jsonOptions);
            if (settings != null) settingsRepo.SaveSettings(settings);
        }

        // State importieren
        if (body.TryGetProperty("state", out var stateEl))
        {
            var state = JsonSerializer.Deserialize<Dictionary<string, bool>>(stateEl.GetRawText(), jsonOptions);
            if (state != null) stateRepo.SaveState(projectId, state);
        }

        // Wochen & Tasks importieren
        if (body.TryGetProperty("weeks", out var weeksEl))
        {
            using var con1 = dbContext.CreateConnection();
            con1.Open();
            using var tx1 = con1.BeginTransaction();

            using var delTasks = con1.CreateCommand();
            delTasks.CommandText = "DELETE FROM tasks WHERE week_number IN (SELECT number FROM weeks WHERE project_id = @pid)";
            delTasks.Parameters.AddWithValue("@pid", projectId);
            delTasks.ExecuteNonQuery();

            using var delWeeks = con1.CreateCommand();
            delWeeks.CommandText = "DELETE FROM weeks WHERE project_id = @pid";
            delWeeks.Parameters.AddWithValue("@pid", projectId);
            delWeeks.ExecuteNonQuery();

            var weeks = JsonSerializer.Deserialize<List<JsonElement>>(weeksEl.GetRawText(), jsonOptions);
            if (weeks != null)
            {
                foreach (var week in weeks)
                {
                    using var wCmd = con1.CreateCommand();
                    wCmd.CommandText = @"
                        INSERT INTO weeks (number, title, phase, badge_pc, badge_phys, note, project_id)
                        VALUES (@n, @t, @p, @bp, @bph, @no, @pid)";
                    wCmd.Parameters.AddWithValue("@pid", projectId);
                    wCmd.Parameters.AddWithValue("@n", week.GetProperty("number").GetInt32());
                    wCmd.Parameters.AddWithValue("@t", week.GetProperty("title").GetString() ?? "");
                    wCmd.Parameters.AddWithValue("@p", week.GetProperty("phase").GetString() ?? "");
                    wCmd.Parameters.AddWithValue("@bp", week.GetProperty("badgePc").GetString() ?? "");
                    wCmd.Parameters.AddWithValue("@bph", week.GetProperty("badgePhys").GetString() ?? "");
                    wCmd.Parameters.AddWithValue("@no",
                        week.TryGetProperty("note", out var note) && note.ValueKind != JsonValueKind.Null
                            ? note.GetString() : (object)DBNull.Value);
                    wCmd.ExecuteNonQuery();

                    if (week.TryGetProperty("tasks", out var tasksEl))
                    {
                        var tasks = JsonSerializer.Deserialize<List<JsonElement>>(tasksEl.GetRawText(), jsonOptions);
                        if (tasks != null)
                        {
                            int sort = 1;
                            foreach (var task in tasks)
                            {
                                using var tCmd = con1.CreateCommand();
                                tCmd.CommandText = @"
                                    INSERT INTO tasks (week_number, sort_order, type, text, hours)
                                    VALUES (@w, @s, @t, @tx, @h)";
                                tCmd.Parameters.AddWithValue("@w", week.GetProperty("number").GetInt32());
                                tCmd.Parameters.AddWithValue("@s", sort++);
                                tCmd.Parameters.AddWithValue("@t", task.GetProperty("type").GetString() ?? "pc");
                                tCmd.Parameters.AddWithValue("@tx", task.GetProperty("text").GetString() ?? "");
                                tCmd.Parameters.AddWithValue("@h", task.GetProperty("hours").GetString() ?? "");
                                tCmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            tx1.Commit();
        }

        // Produktkatalog importieren — separate Connection
        if (body.TryGetProperty("catalog", out var catalogEl))
        {
            using var con2 = dbContext.CreateConnection();
            con2.Open();
            using var tx2 = con2.BeginTransaction();

            using var delVar = con2.CreateCommand(); delVar.CommandText = "DELETE FROM product_variations"; delVar.ExecuteNonQuery();
            using var delProd = con2.CreateCommand(); delProd.CommandText = "DELETE FROM products_v2"; delProd.ExecuteNonQuery();
            using var delAttr = con2.CreateCommand(); delAttr.CommandText = "DELETE FROM product_attributes"; delAttr.ExecuteNonQuery();
            using var delCat = con2.CreateCommand(); delCat.CommandText = "DELETE FROM product_categories"; delCat.ExecuteNonQuery();

            if (catalogEl.TryGetProperty("categories", out var catsEl))
            {
                var cats = JsonSerializer.Deserialize<List<JsonElement>>(catsEl.GetRawText(), jsonOptions);
                if (cats != null)
                {
                    foreach (var cat in cats)
                    {
                        using var cCmd = con2.CreateCommand();
                        cCmd.CommandText = "INSERT INTO product_categories (id, name, description, color) VALUES (@id, @n, @d, @c)";
                        cCmd.Parameters.AddWithValue("@id", cat.GetProperty("id").GetInt32());
                        cCmd.Parameters.AddWithValue("@n", cat.GetProperty("name").GetString() ?? "");
                        cCmd.Parameters.AddWithValue("@d", cat.TryGetProperty("description", out var cd) && cd.ValueKind != JsonValueKind.Null ? cd.GetString() : (object)DBNull.Value);
                        cCmd.Parameters.AddWithValue("@c", cat.TryGetProperty("color", out var cc) ? cc.GetString() ?? "#4f8ef7" : "#4f8ef7");
                        cCmd.ExecuteNonQuery();

                        if (cat.TryGetProperty("attributes", out var attrsEl))
                        {
                            var attrs = JsonSerializer.Deserialize<List<JsonElement>>(attrsEl.GetRawText(), jsonOptions);
                            if (attrs != null)
                            {
                                foreach (var attr in attrs)
                                {
                                    using var aCmd = con2.CreateCommand();
                                    aCmd.CommandText = "INSERT INTO product_attributes (id, category_id, name, field_type, options, required, sort_order) VALUES (@id, @c, @n, @ft, @o, @r, @s)";
                                    aCmd.Parameters.AddWithValue("@id", attr.GetProperty("id").GetInt32());
                                    aCmd.Parameters.AddWithValue("@c", cat.GetProperty("id").GetInt32());
                                    aCmd.Parameters.AddWithValue("@n", attr.GetProperty("name").GetString() ?? "");
                                    aCmd.Parameters.AddWithValue("@ft", attr.TryGetProperty("fieldType", out var ft) ? ft.GetString() ?? "text" : "text");
                                    aCmd.Parameters.AddWithValue("@o", attr.TryGetProperty("options", out var ao) && ao.ValueKind != JsonValueKind.Null ? ao.GetString() : (object)DBNull.Value);
                                    aCmd.Parameters.AddWithValue("@r", attr.TryGetProperty("required", out var ar) && ar.GetBoolean() ? 1 : 0);
                                    aCmd.Parameters.AddWithValue("@s", attr.TryGetProperty("sortOrder", out var as2) ? as2.GetInt32() : 0);
                                    aCmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }
            }

            if (catalogEl.TryGetProperty("products", out var prodsEl))
            {
                var prods = JsonSerializer.Deserialize<List<JsonElement>>(prodsEl.GetRawText(), jsonOptions);
                if (prods != null)
                {
                    foreach (var prod in prods)
                    {
                        // attributeValues: entweder JSON-Objekt oder String — beide Fälle abfangen
                        string attrValuesStr = "{}";
                        if (prod.TryGetProperty("attributeValues", out var av))
                        {
                            attrValuesStr = av.ValueKind == JsonValueKind.String
                                ? av.GetString() ?? "{}"
                                : av.GetRawText();
                        }

                        using var pCmd = con2.CreateCommand();
                        pCmd.CommandText = "INSERT INTO products_v2 (id, category_id, name, description, attribute_values, created_at) VALUES (@id, @c, @n, @d, @av, @ca)";
                        pCmd.Parameters.AddWithValue("@id", prod.GetProperty("id").GetInt32());
                        pCmd.Parameters.AddWithValue("@c", prod.GetProperty("categoryId").GetInt32());
                        pCmd.Parameters.AddWithValue("@n", prod.GetProperty("name").GetString() ?? "");
                        pCmd.Parameters.AddWithValue("@d", prod.TryGetProperty("description", out var pd) && pd.ValueKind != JsonValueKind.Null ? pd.GetString() : (object)DBNull.Value);
                        pCmd.Parameters.AddWithValue("@av", attrValuesStr);
                        pCmd.Parameters.AddWithValue("@ca", prod.TryGetProperty("createdAt", out var pca) && pca.ValueKind == JsonValueKind.String
                            ? pca.GetString() ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                            : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        pCmd.ExecuteNonQuery();

                        if (prod.TryGetProperty("variations", out var varsEl))
                        {
                            var vars = JsonSerializer.Deserialize<List<JsonElement>>(varsEl.GetRawText(), jsonOptions);
                            if (vars != null)
                            {
                                foreach (var v in vars)
                                {
                                    using var vCmd = con2.CreateCommand();
                                    vCmd.CommandText = "INSERT INTO product_variations (id, product_id, name, sku, price, stock, created_at) VALUES (@id, @p, @n, @s, @pr, @st, @ca)";
                                    vCmd.Parameters.AddWithValue("@id", v.GetProperty("id").GetInt32());
                                    vCmd.Parameters.AddWithValue("@p", prod.GetProperty("id").GetInt32());
                                    vCmd.Parameters.AddWithValue("@n", v.GetProperty("name").GetString() ?? "");
                                    vCmd.Parameters.AddWithValue("@s", v.GetProperty("sku").GetString() ?? "");
                                    vCmd.Parameters.AddWithValue("@pr", v.TryGetProperty("price", out var vp) ? vp.GetDecimal() : 0);
                                    vCmd.Parameters.AddWithValue("@st", v.TryGetProperty("stock", out var vs) ? vs.GetInt32() : 0);
                                    vCmd.Parameters.AddWithValue("@ca", v.TryGetProperty("createdAt", out var vca) && vca.ValueKind == JsonValueKind.String
                                        ? vca.GetString() ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                                        : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                    vCmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }
            }
            tx2.Commit();
        }

        return Results.Ok(new { imported = true });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// GET /api/catalog
app.MapGet("/api/catalog", (HttpRequest req, IProductCatalogRepository repo) =>
    Results.Ok(repo.GetAll(GetProjectId(req))));

// ── KATEGORIEN ──
app.MapPost("/api/catalog/categories", async (HttpRequest request, IProductCatalogRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
    var name = body.GetProperty("name").GetString() ?? "";
    var desc = body.TryGetProperty("description", out var d) ? d.GetString() : null;
    var color = body.TryGetProperty("color", out var c) ? c.GetString() ?? "#4f8ef7" : "#4f8ef7";
    return Results.Ok(repo.CreateCategory(GetProjectId(request), name, desc, color));
});

app.MapPut("/api/catalog/categories/{id}", async (int id, HttpRequest request, IProductCatalogRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
    var name = body.GetProperty("name").GetString() ?? "";
    var desc = body.TryGetProperty("description", out var d) ? d.GetString() : null;
    var color = body.TryGetProperty("color", out var c) ? c.GetString() ?? "#4f8ef7" : "#4f8ef7";
    return Results.Ok(repo.UpdateCategory(id, name, desc, color));
});

app.MapDelete("/api/catalog/categories/{id}", (int id, IProductCatalogRepository repo) =>
{
    repo.DeleteCategory(id);
    return Results.Ok(new { deleted = true });
});

// ── ATTRIBUTE ──
app.MapPost("/api/catalog/categories/{id}/attributes", async (int id, HttpRequest request, IProductCatalogRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
    var name = body.GetProperty("name").GetString() ?? "";
    var fieldType = body.TryGetProperty("fieldType", out var ft) ? ft.GetString() ?? "text" : "text";
    var options = body.TryGetProperty("options", out var o) ? o.GetString() : null;
    var required = body.TryGetProperty("required", out var r) && r.GetBoolean();
    var sortOrder = body.TryGetProperty("sortOrder", out var s) ? s.GetInt32() : 0;
    return Results.Ok(repo.AddAttribute(id, name, fieldType, options, required, sortOrder));
});

app.MapDelete("/api/catalog/attributes/{id}", (int id, IProductCatalogRepository repo) =>
{
    repo.DeleteAttribute(id);
    return Results.Ok(new { deleted = true });
});

// ── PRODUKTE ──
app.MapPost("/api/catalog/products", async (HttpRequest request, IProductCatalogRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
    var categoryId = body.GetProperty("categoryId").GetInt32();
    var name = body.GetProperty("name").GetString() ?? "";
    var desc = body.TryGetProperty("description", out var d) ? d.GetString() : null;
    var attributeValues = body.TryGetProperty("attributeValues", out var av) ? av.GetRawText() : "{}";
    return Results.Ok(repo.CreateProduct(categoryId, name, desc, attributeValues));
});

app.MapPut("/api/catalog/products/{id}", async (int id, HttpRequest request, IProductCatalogRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
    var name = body.GetProperty("name").GetString() ?? "";
    var desc = body.TryGetProperty("description", out var d) ? d.GetString() : null;
    var attributeValues = body.TryGetProperty("attributeValues", out var av) ? av.GetRawText() : "{}";
    return Results.Ok(repo.UpdateProduct(id, name, desc, attributeValues));
});

app.MapDelete("/api/catalog/products/{id}", (int id, IProductCatalogRepository repo) =>
{
    repo.DeleteProduct(id);
    return Results.Ok(new { deleted = true });
});

// ── VARIATIONEN ──
app.MapGet("/api/catalog/products/{id}/sku", (int id, string variationName, HttpRequest request, IProductCatalogRepository repo) =>
{
    var product = repo.GetAll(GetProjectId(request)).Products.FirstOrDefault(p => p.Id == id);
    if (product == null) return Results.NotFound();
    var sku = repo.GenerateSku(product.CategoryId, id, variationName);
    return Results.Ok(new { sku });
});

app.MapPost("/api/catalog/variations", async (HttpRequest request, IProductCatalogRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
    var productId = body.GetProperty("productId").GetInt32();
    var name = body.GetProperty("name").GetString() ?? "";
    var sku = body.GetProperty("sku").GetString() ?? "";
    var price = body.GetProperty("price").GetDecimal();
    var stock = body.TryGetProperty("stock", out var st) ? st.GetInt32() : 0;

    if (repo.SkuExists(sku))
        return Results.BadRequest(new { error = $"SKU '{sku}' existiert bereits." });

    return Results.Ok(repo.AddVariation(productId, name, sku, price, stock));
});

app.MapPut("/api/catalog/variations/{id}", async (int id, HttpRequest request, IProductCatalogRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
    var name = body.GetProperty("name").GetString() ?? "";
    var sku = body.GetProperty("sku").GetString() ?? "";
    var price = body.GetProperty("price").GetDecimal();
    var stock = body.TryGetProperty("stock", out var st) ? st.GetInt32() : 0;

    if (repo.SkuExists(sku, id))
        return Results.BadRequest(new { error = $"SKU '{sku}' existiert bereits." });

    return Results.Ok(repo.UpdateVariation(id, name, sku, price, stock));
});

app.MapDelete("/api/catalog/variations/{id}", (int id, IProductCatalogRepository repo) =>
{
    repo.DeleteVariation(id);
    return Results.Ok(new { deleted = true });
});

// ── PRODUKTION ──

// GET /api/production
app.MapGet("/api/production", (HttpRequest req, IProductionRepository repo) =>
    Results.Ok(repo.GetAll(GetProjectId(req))));

// POST /api/production
app.MapPost("/api/production", async (HttpRequest request, IProductionRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
    var productId   = body.GetProperty("productId").GetInt32();
    var variationId = body.TryGetProperty("variationId", out var vid) && vid.ValueKind != JsonValueKind.Null ? vid.GetInt32() : (int?)null;
    var quantity    = body.TryGetProperty("quantity", out var qty) ? qty.GetInt32() : 1;
    var note        = body.TryGetProperty("note", out var n) && n.ValueKind != JsonValueKind.Null ? n.GetString() : null;
    return Results.Ok(repo.Add(GetProjectId(request), productId, variationId, quantity, note));
});

// PATCH /api/production/{id}/done
app.MapMethods("/api/production/{id}/done", ["PATCH"], async (int id, HttpRequest request, IProductionRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
    var done = body.GetProperty("done").GetBoolean();
    repo.SetDone(id, done);
    return Results.Ok(new { updated = true });
});

// PUT /api/production/{id}
app.MapPut("/api/production/{id}", async (int id, HttpRequest request, IProductionRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
    var quantity = body.GetProperty("quantity").GetInt32();
    var note     = body.TryGetProperty("note", out var n) && n.ValueKind != JsonValueKind.Null ? n.GetString() : null;
    repo.UpdateItem(id, quantity, note);
    return Results.Ok(new { updated = true });
});

// DELETE /api/production/done  (muss vor /{id} stehen!)
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

// ── KALENDER ──

// GET /api/calendar
app.MapGet("/api/calendar", (HttpRequest req, ICalendarRepository repo) =>
    Results.Ok(repo.GetAll(GetProjectId(req))));

// POST /api/calendar
app.MapPost("/api/calendar", async (HttpRequest request, ICalendarRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
    var title       = body.GetProperty("title").GetString() ?? "";
    var date        = body.GetProperty("date").GetString() ?? "";
    var endDate     = body.TryGetProperty("endDate", out var ed) && ed.ValueKind != JsonValueKind.Null ? ed.GetString() : null;
    var time        = body.TryGetProperty("time", out var t) && t.ValueKind != JsonValueKind.Null ? t.GetString() : null;
    var description = body.TryGetProperty("description", out var d) && d.ValueKind != JsonValueKind.Null ? d.GetString() : null;
    var color       = body.TryGetProperty("color", out var c) && c.ValueKind != JsonValueKind.Null ? c.GetString() ?? "#4f8ef7" : "#4f8ef7";
    var type        = body.TryGetProperty("type", out var ty) && ty.ValueKind != JsonValueKind.Null ? ty.GetString() ?? "event" : "event";
    return Results.Ok(repo.Add(GetProjectId(request), title, date, endDate, time, description, color, type));
});

// PUT /api/calendar/{id}
app.MapPut("/api/calendar/{id}", async (int id, HttpRequest request, ICalendarRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
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

// ── PROJEKTE ──

// GET /api/projects — Projekte des eingeloggten Users
app.MapGet("/api/projects", (HttpContext ctx, IUserRepository userRepo, IProjectRepository projectRepo) =>
{
    var user = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
    if (user == null) return Results.Unauthorized();
    return Results.Ok(projectRepo.GetForUser(user.Id));
});

// POST /api/projects — neues Projekt erstellen (alle authentifizierten User)
app.MapPost("/api/projects", async (HttpRequest request, HttpContext ctx, IUserRepository userRepo, IProjectRepository projectRepo) =>
{
    var user = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
    if (user == null) return Results.Unauthorized();
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
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
    if (role != "admin" && !ctx.User.IsInRole("admin")) return Results.Forbid();
    var body         = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
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
    if (role != "admin" && !ctx.User.IsInRole("admin")) return Results.Forbid();
    var body     = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
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
    if (role != "admin" && !ctx.User.IsInRole("admin")) return Results.Forbid();
    if (userId == currentUser.Id) return Results.BadRequest(new { error = "Du kannst dich nicht selbst entfernen." });
    projectRepo.RemoveMember(id, userId);
    return Results.Ok(new { removed = true });
});

// POST /api/projects/{id}/invites — Einladungslink erstellen
app.MapPost("/api/projects/{id}/invites", async (int id, HttpRequest request, HttpContext ctx, IUserRepository userRepo, IProjectRepository projectRepo, IInviteRepository inviteRepo) =>
{
    var currentUser = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
    if (currentUser == null) return Results.Unauthorized();
    var role = projectRepo.GetRole(id, currentUser.Id);
    if (role != "admin" && !ctx.User.IsInRole("admin")) return Results.Forbid();
    var body       = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
    var inviteRole = body.TryGetProperty("role", out var r) ? r.GetString() ?? "member" : "member";
    var hours      = body.TryGetProperty("hoursValid", out var h) ? h.GetInt32() : 48;
    var invite     = inviteRepo.Create("project", id, inviteRole, currentUser.Id, hours);
    return Results.Ok(invite);
});

// POST /api/invites/{token}/accept — Einladung annehmen
app.MapPost("/api/invites/{token}/accept", async (string token, HttpRequest request, HttpContext ctx, IUserRepository userRepo, IProjectRepository projectRepo, IInviteRepository inviteRepo) =>
{
    var invite = inviteRepo.GetByToken(token);
    if (invite == null) return Results.NotFound(new { error = "Einladung nicht gefunden." });
    if (invite.UsedAt != null) return Results.BadRequest(new { error = "Einladung wurde bereits verwendet." });
    if (DateTime.TryParse(invite.ExpiresAt, out var expires) && expires < DateTime.UtcNow)
        return Results.BadRequest(new { error = "Einladung ist abgelaufen." });

    // Eingeloggten User dem Projekt hinzufügen
    var currentUser = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
    if (currentUser == null) return Results.Unauthorized();

    if (invite.ProjectId.HasValue)
        projectRepo.AddMember(invite.ProjectId.Value, currentUser.Id, invite.Role);

    inviteRepo.MarkUsed(token);
    return Results.Ok(new { accepted = true, projectId = invite.ProjectId });
});

// POST /api/projects/{id}/leave — Projekt selbst verlassen
app.MapPost("/api/projects/{id}/leave", (int id, HttpContext ctx, IUserRepository userRepo, IProjectRepository projectRepo) =>
{
    var user = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
    if (user == null) return Results.Unauthorized();
    if (!projectRepo.IsMember(id, user.Id)) return Results.BadRequest(new { error = "Nicht Mitglied." });
    projectRepo.RemoveMember(id, user.Id);
    return Results.Ok(new { left = true });
});

// POST /api/platform/invites — BizHub-Einladungslink generieren (nur Platform-Admin)
app.MapPost("/api/platform/invites", async (HttpRequest request, HttpContext ctx, IUserRepository userRepo, IInviteRepository inviteRepo) =>
{
    if (!ctx.User.IsInRole("admin")) return Results.Forbid();
    var user = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
    if (user == null) return Results.Unauthorized();
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
    var hours = body.TryGetProperty("hoursValid", out var h) && h.TryGetInt32(out var hv) ? hv : 48;
    var invite = inviteRepo.Create("platform", null, "user", user.Id, hours);
    return Results.Ok(invite);
});

// POST /api/auth/register — Account erstellen via Platform-Invite-Token
app.MapPost("/api/auth/register", async (HttpRequest request, IUserRepository userRepo, IInviteRepository inviteRepo) =>
{
    var body     = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
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

app.Run();