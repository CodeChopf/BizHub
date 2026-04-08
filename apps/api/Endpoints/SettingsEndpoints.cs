using System.Text.Json;
using AuraPrintsApi.Data;
using AuraPrintsApi.Models;
using AuraPrintsApi.Repositories;

namespace AuraPrintsApi.Endpoints;

public static class SettingsEndpoints
{
    public static WebApplication MapSettingsEndpoints(this WebApplication app)
    {
        // GET /api/version
        app.MapGet("/api/version", () =>
            Results.Ok(new { version = Environment.GetEnvironmentVariable("BIZHUB_VERSION") ?? "dev" })
        ).AllowAnonymous();

        // GET /api/settings
        app.MapGet("/api/settings", (HttpRequest request, ISettingsRepository repo) =>
            Results.Ok(repo.GetSettings(ApiHelpers.GetProjectId(request))));

        // POST /api/settings
        app.MapPost("/api/settings", async (HttpRequest request, ISettingsRepository repo, IProjectRepository projectRepo) =>
        {
            var projectId = ApiHelpers.GetProjectId(request);
            var settings = await JsonSerializer.DeserializeAsync<ProjectSettings>(request.Body, ApiHelpers.JsonOptions);
            if (settings == null) return Results.BadRequest();
            repo.SaveSettings(projectId, settings);
            projectRepo.Update(projectId, settings.ProjectName, settings.Description, settings.StartDate, settings.Currency ?? "CHF", settings.ProjectImage, settings.VisibleTabs);
            return Results.Ok(repo.GetSettings(projectId));
        });

        // GET /api/export
        app.MapGet("/api/export", (HttpRequest request, ISettingsRepository settingsRepo, IRoadmapRepository roadmapRepo, IExpenseRepository expenseRepo, IStateRepository stateRepo, IProductCatalogRepository catalogRepo) =>
        {
            var projectId = ApiHelpers.GetProjectId(request);
            var settings = settingsRepo.GetSettings(projectId);
            var roadmap = roadmapRepo.GetAll(projectId);
            var finance = expenseRepo.GetAll(projectId);
            var state = stateRepo.GetState(projectId);
            var catalog = catalogRepo.GetAll(projectId);

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
            IRoadmapRepository roadmapRepo,
            DatabaseContext dbContext) =>
        {
            try
            {
                var projectId = ApiHelpers.GetProjectId(request);
                var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);

                if (body.TryGetProperty("settings", out var settingsEl))
                {
                    var settings = JsonSerializer.Deserialize<ProjectSettings>(settingsEl.GetRawText(), ApiHelpers.JsonOptions);
                    if (settings != null) settingsRepo.SaveSettings(projectId, settings);
                }

                if (body.TryGetProperty("state", out var stateEl))
                {
                    var state = JsonSerializer.Deserialize<Dictionary<string, bool>>(stateEl.GetRawText(), ApiHelpers.JsonOptions);
                    if (state != null) stateRepo.SaveState(projectId, state);
                }

                if (body.TryGetProperty("weeks", out var weeksEl))
                {
                    using var con1 = dbContext.CreateConnection();
                    con1.Open();
                    using var tx1 = con1.BeginTransaction();

                    using var delSubs = con1.CreateCommand();
                    delSubs.CommandText = "DELETE FROM subtasks WHERE task_id IN (SELECT id FROM tasks WHERE project_id = @pid)";
                    delSubs.Parameters.AddWithValue("@pid", projectId);
                    delSubs.ExecuteNonQuery();

                    using var delTasks = con1.CreateCommand();
                    delTasks.CommandText = "DELETE FROM tasks WHERE week_number IN (SELECT number FROM weeks WHERE project_id = @pid)";
                    delTasks.Parameters.AddWithValue("@pid", projectId);
                    delTasks.ExecuteNonQuery();

                    using var delWeeks = con1.CreateCommand();
                    delWeeks.CommandText = "DELETE FROM weeks WHERE project_id = @pid";
                    delWeeks.Parameters.AddWithValue("@pid", projectId);
                    delWeeks.ExecuteNonQuery();

                    var weeks = JsonSerializer.Deserialize<List<JsonElement>>(weeksEl.GetRawText(), ApiHelpers.JsonOptions);
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
                                var tasks = JsonSerializer.Deserialize<List<JsonElement>>(tasksEl.GetRawText(), ApiHelpers.JsonOptions);
                                if (tasks != null)
                                {
                                    int sort = 1;
                                    foreach (var task in tasks)
                                    {
                                        using var tCmd = con1.CreateCommand();
                                        tCmd.CommandText = @"
                                            INSERT INTO tasks (week_number, sort_order, type, text, hours, project_id)
                                            VALUES (@w, @s, @t, @tx, @h, @pid);
                                            SELECT last_insert_rowid();";
                                        tCmd.Parameters.AddWithValue("@w",   week.GetProperty("number").GetInt32());
                                        tCmd.Parameters.AddWithValue("@s",   sort++);
                                        tCmd.Parameters.AddWithValue("@t",   task.GetProperty("type").GetString() ?? "pc");
                                        tCmd.Parameters.AddWithValue("@tx",  task.GetProperty("text").GetString() ?? "");
                                        tCmd.Parameters.AddWithValue("@h",   task.GetProperty("hours").GetString() ?? "");
                                        tCmd.Parameters.AddWithValue("@pid", projectId);
                                        var newTaskId = (long)(tCmd.ExecuteScalar() ?? 0L);

                                        if (task.TryGetProperty("subtasks", out var subtasksEl) && subtasksEl.ValueKind == JsonValueKind.Array)
                                        {
                                            var subtasks = JsonSerializer.Deserialize<List<JsonElement>>(subtasksEl.GetRawText(), ApiHelpers.JsonOptions);
                                            if (subtasks != null)
                                            {
                                                int subSort = 1;
                                                foreach (var sub in subtasks)
                                                {
                                                    using var sCmd = con1.CreateCommand();
                                                    sCmd.CommandText = @"
                                                        INSERT INTO subtasks (task_id, sort_order, text, hours, project_id)
                                                        VALUES (@tid, @s, @tx, @h, @pid)";
                                                    sCmd.Parameters.AddWithValue("@tid", newTaskId);
                                                    sCmd.Parameters.AddWithValue("@s",   subSort++);
                                                    sCmd.Parameters.AddWithValue("@tx",  sub.TryGetProperty("text", out var st) ? st.GetString() ?? "" : "");
                                                    sCmd.Parameters.AddWithValue("@h",   sub.TryGetProperty("hours", out var sh) ? sh.GetString() ?? "" : "");
                                                    sCmd.Parameters.AddWithValue("@pid", projectId);
                                                    sCmd.ExecuteNonQuery();
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    tx1.Commit();
                }

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
                        var cats = JsonSerializer.Deserialize<List<JsonElement>>(catsEl.GetRawText(), ApiHelpers.JsonOptions);
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
                                    var attrs = JsonSerializer.Deserialize<List<JsonElement>>(attrsEl.GetRawText(), ApiHelpers.JsonOptions);
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
                        var prods = JsonSerializer.Deserialize<List<JsonElement>>(prodsEl.GetRawText(), ApiHelpers.JsonOptions);
                        if (prods != null)
                        {
                            foreach (var prod in prods)
                            {
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
                                    var vars = JsonSerializer.Deserialize<List<JsonElement>>(varsEl.GetRawText(), ApiHelpers.JsonOptions);
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

        return app;
    }
}
