using System.Text.Json;
using AuraPrintsApi.Data;
using AuraPrintsApi.Models;
using AuraPrintsApi.Repositories;

var builder = WebApplication.CreateBuilder(args);

var dbFile = Path.Combine(AppContext.BaseDirectory, "Data", "auraprints.db");
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

var app = builder.Build();

var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    WriteIndented = true
};

app.UseDefaultFiles();
app.UseStaticFiles();

var seeder = new DatabaseSeeder(dbContext);
dbContext.Initialize();
seeder.Seed();

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

// Browser öffnen
var url = app.Urls.FirstOrDefault() ?? "http://localhost:5000";
app.Lifetime.ApplicationStarted.Register(() =>
{
    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    {
        FileName = url,
        UseShellExecute = true
    });
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

// GET /api/admin/tasks/ids — Task-IDs einer Woche
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

// GET /api/export
app.MapGet("/api/export", (ISettingsRepository settingsRepo, IRoadmapRepository roadmapRepo, IProductRepository productRepo, IExpenseRepository expenseRepo, IStateRepository stateRepo) =>
{
    var settings = settingsRepo.GetSettings();
    var roadmap = roadmapRepo.GetAll();
    var finance = expenseRepo.GetAll();
    var state = stateRepo.GetState();

    var export = new
    {
        exportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        version = "1.0",
        settings,
        state,
        weeks = roadmap.Weeks,
        finance
    };

    var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
    var bytes = System.Text.Encoding.UTF8.GetBytes(json);
    var fileName = $"{settings.ProjectName.Replace(" ", "_")}_{DateTime.Now:yyyy-MM-dd}.json";
    return Results.File(bytes, "application/json", fileName);
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
            using var con = dbContext.CreateConnection();
            con.Open();
            using var tx = con.BeginTransaction();

            // Bestehende Wochen und Tasks löschen
            using var delTasks = con.CreateCommand();
            delTasks.CommandText = "DELETE FROM tasks";
            delTasks.ExecuteNonQuery();

            using var delWeeks = con.CreateCommand();
            delWeeks.CommandText = "DELETE FROM weeks";
            delWeeks.ExecuteNonQuery();

            var weeks = JsonSerializer.Deserialize<List<JsonElement>>(weeksEl.GetRawText(), jsonOptions);
            if (weeks != null)
            {
                foreach (var week in weeks)
                {
                    using var wCmd = con.CreateCommand();
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
                                using var tCmd = con.CreateCommand();
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
            tx.Commit();
        }

        return Results.Ok(new { imported = true });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();