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
app.MapGet("/api/data", (IRoadmapRepository repo) =>
    Results.Ok(repo.GetAll()));

// GET /api/products
app.MapGet("/api/products", (IProductRepository repo) =>
    Results.Ok(repo.GetAll()));

// GET /api/state
app.MapGet("/api/state", (IStateRepository repo) =>
    Results.Ok(repo.GetState()));

// POST /api/state
app.MapPost("/api/state", async (HttpRequest request, IStateRepository repo) =>
{
    var state = await JsonSerializer.DeserializeAsync<Dictionary<string, bool>>(request.Body);
    if (state == null) return Results.BadRequest();
    repo.SaveState(state);
    return Results.Ok(new { saved = true });
});

// GET /api/finance
app.MapGet("/api/finance", (IExpenseRepository repo) =>
    Results.Ok(repo.GetAll()));

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
    var expense = repo.Add(categoryId, amount, description, link, date, weekNumber, taskId);
    return Results.Ok(expense);
});

// DELETE /api/expenses/{id}
app.MapDelete("/api/expenses/{id}", (int id, IExpenseRepository repo) =>
{
    repo.Delete(id);
    return Results.Ok(new { deleted = true });
});

// GET /api/categories
app.MapGet("/api/categories", (ICategoryRepository repo) =>
    Results.Ok(repo.GetAll()));

// POST /api/categories
app.MapPost("/api/categories", async (HttpRequest request, ICategoryRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
    var name = body.GetProperty("name").GetString() ?? "";
    var color = body.GetProperty("color").GetString() ?? "#9699a8";
    var cat = repo.Add(name, color);
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
    return Results.Ok(repo.CreateWeek(req));
});

app.MapPut("/api/admin/weeks/{number}", async (int number, HttpRequest request, IAdminRepository repo) =>
{
    var req = await JsonSerializer.DeserializeAsync<UpdateWeekRequest>(request.Body, jsonOptions);
    if (req == null) return Results.BadRequest();
    return Results.Ok(repo.UpdateWeek(number, req));
});

app.MapDelete("/api/admin/weeks/{number}", (int number, IAdminRepository repo) =>
{
    repo.DeleteWeek(number);
    return Results.Ok(new { deleted = true });
});

// ── ADMIN: TASKS ──
app.MapPost("/api/admin/tasks", async (HttpRequest request, IAdminRepository repo) =>
{
    var req = await JsonSerializer.DeserializeAsync<CreateTaskRequest>(request.Body, jsonOptions);
    if (req == null) return Results.BadRequest();
    return Results.Ok(repo.CreateTask(req));
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
    repo.ReorderTasks(number, req);
    return Results.Ok(new { reordered = true });
});

// GET /api/admin/tasks/ids
app.MapGet("/api/admin/tasks/ids", (int weekNumber, IRoadmapRepository roadmapRepo) =>
{
    using var con = dbContext.CreateConnection();
    con.Open();
    using var cmd = con.CreateCommand();
    cmd.CommandText = "SELECT id FROM tasks WHERE week_number = @w ORDER BY sort_order";
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
app.MapGet("/api/milestones", (IMilestoneRepository repo) =>
    Results.Ok(repo.GetAll()));

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
    var milestone = repo.Create(name, description, snapshot);
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
app.MapGet("/api/export", (ISettingsRepository settingsRepo, IRoadmapRepository roadmapRepo, IExpenseRepository expenseRepo, IStateRepository stateRepo, IProductCatalogRepository catalogRepo) =>
{
    var settings = settingsRepo.GetSettings();
    var roadmap = roadmapRepo.GetAll();
    var finance = expenseRepo.GetAll();
    var state = stateRepo.GetState();
    var catalog = catalogRepo.GetAll();

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
            if (state != null) stateRepo.SaveState(state);
        }

        // Wochen & Tasks importieren
        if (body.TryGetProperty("weeks", out var weeksEl))
        {
            using var con1 = dbContext.CreateConnection();
            con1.Open();
            using var tx1 = con1.BeginTransaction();

            using var delTasks = con1.CreateCommand();
            delTasks.CommandText = "DELETE FROM tasks";
            delTasks.ExecuteNonQuery();

            using var delWeeks = con1.CreateCommand();
            delWeeks.CommandText = "DELETE FROM weeks";
            delWeeks.ExecuteNonQuery();

            var weeks = JsonSerializer.Deserialize<List<JsonElement>>(weeksEl.GetRawText(), jsonOptions);
            if (weeks != null)
            {
                foreach (var week in weeks)
                {
                    using var wCmd = con1.CreateCommand();
                    wCmd.CommandText = @"
                        INSERT INTO weeks (number, title, phase, badge_pc, badge_phys, note)
                        VALUES (@n, @t, @p, @bp, @bph, @no)";
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
app.MapGet("/api/catalog", (IProductCatalogRepository repo) =>
    Results.Ok(repo.GetAll()));

// ── KATEGORIEN ──
app.MapPost("/api/catalog/categories", async (HttpRequest request, IProductCatalogRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
    var name = body.GetProperty("name").GetString() ?? "";
    var desc = body.TryGetProperty("description", out var d) ? d.GetString() : null;
    var color = body.TryGetProperty("color", out var c) ? c.GetString() ?? "#4f8ef7" : "#4f8ef7";
    return Results.Ok(repo.CreateCategory(name, desc, color));
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
app.MapGet("/api/catalog/products/{id}/sku", (int id, string variationName, IProductCatalogRepository repo) =>
{
    var product = repo.GetAll().Products.FirstOrDefault(p => p.Id == id);
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
app.MapGet("/api/production", (IProductionRepository repo) =>
    Results.Ok(repo.GetAll()));

// POST /api/production
app.MapPost("/api/production", async (HttpRequest request, IProductionRepository repo) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, jsonOptions);
    var productId   = body.GetProperty("productId").GetInt32();
    var variationId = body.TryGetProperty("variationId", out var vid) && vid.ValueKind != JsonValueKind.Null ? vid.GetInt32() : (int?)null;
    var quantity    = body.TryGetProperty("quantity", out var qty) ? qty.GetInt32() : 1;
    var note        = body.TryGetProperty("note", out var n) && n.ValueKind != JsonValueKind.Null ? n.GetString() : null;
    return Results.Ok(repo.Add(productId, variationId, quantity, note));
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
app.MapGet("/api/calendar", (ICalendarRepository repo) =>
    Results.Ok(repo.GetAll()));

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
    return Results.Ok(repo.Add(title, date, endDate, time, description, color, type));
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

app.Run();