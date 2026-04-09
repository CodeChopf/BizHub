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
        app.MapGet("/api/settings", (HttpRequest request, HttpContext ctx, ISettingsRepository repo, IUserRepository userRepo, IProjectRepository projectRepo) =>
        {
            var user = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
            if (user == null) return Results.Unauthorized();
            var projectId = ApiHelpers.GetProjectId(request);
            if (!projectRepo.IsMember(projectId, user.Id)) return Results.Forbid();
            return Results.Ok(repo.GetSettings(projectId));
        });

        // POST /api/settings
        app.MapPost("/api/settings", async (HttpRequest request, HttpContext ctx, ISettingsRepository repo, IProjectRepository projectRepo, IUserRepository userRepo) =>
        {
            var projectId = ApiHelpers.GetProjectId(request);
            var user = userRepo.GetByUsername(ctx.User.Identity?.Name ?? "");
            if (user == null) return Results.Unauthorized();
            var role = projectRepo.GetRole(projectId, user.Id);
            if (role != "admin") return Results.Forbid();
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
                var warnings = new List<string>();

                if (body.TryGetProperty("settings", out var settingsEl))
                {
                    try
                    {
                        var settings = ParseImportSettings(settingsEl);
                        settingsRepo.SaveSettings(projectId, settings);
                    }
                    catch (Exception ex)
                    {
                        warnings.Add("settings: " + ex.Message);
                    }
                }

                if (body.TryGetProperty("state", out var stateEl))
                {
                    try
                    {
                        var state = JsonSerializer.Deserialize<Dictionary<string, bool>>(stateEl.GetRawText(), ApiHelpers.JsonOptions);
                        if (state != null) stateRepo.SaveState(projectId, state);
                    }
                    catch (Exception ex)
                    {
                        warnings.Add("state: " + ex.Message);
                    }
                }

                if (body.TryGetProperty("weeks", out var weeksEl))
                {
                    try
                    {
                        using var con1 = dbContext.CreateConnection();
                        con1.Open();

                        // Legacy DBs may still have a tasks->weeks FK on week_number.
                        // During project-scoped re-import this can trigger "foreign key mismatch".
                        using (var fkOff = con1.CreateCommand())
                        {
                            fkOff.CommandText = "PRAGMA foreign_keys = OFF";
                            fkOff.ExecuteNonQuery();
                        }

                        using var tx1 = con1.BeginTransaction();

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
                                var weekNumber = ReadInt(week, "number");
                                if (weekNumber == null) continue;
                                using var wCmd = con1.CreateCommand();
                                wCmd.CommandText = @"
                                INSERT INTO weeks (number, title, phase, badge_pc, badge_phys, note, project_id)
                                VALUES (@n, @t, @p, @bp, @bph, @no, @pid)";
                                wCmd.Parameters.AddWithValue("@pid", projectId);
                                wCmd.Parameters.AddWithValue("@n", weekNumber.Value);
                                wCmd.Parameters.AddWithValue("@t", ReadString(week, "title") ?? "");
                                wCmd.Parameters.AddWithValue("@p", ReadString(week, "phase") ?? "");
                                wCmd.Parameters.AddWithValue("@bp", ReadString(week, "badgePc", "badge_pc") ?? "");
                                wCmd.Parameters.AddWithValue("@bph", ReadString(week, "badgePhys", "badge_phys") ?? "");
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
                                            VALUES (@w, @s, @t, @tx, @h, @pid)";
                                            tCmd.Parameters.AddWithValue("@pid", projectId);
                                            tCmd.Parameters.AddWithValue("@w", weekNumber.Value);
                                            tCmd.Parameters.AddWithValue("@s", sort++);
                                            tCmd.Parameters.AddWithValue("@t", ReadString(task, "type") ?? "pc");
                                            tCmd.Parameters.AddWithValue("@tx", ReadString(task, "text") ?? "");
                                            tCmd.Parameters.AddWithValue("@h", ReadString(task, "hours") ?? "");
                                            tCmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }
                        }
                        tx1.Commit();

                        using (var fkOn = con1.CreateCommand())
                        {
                            fkOn.CommandText = "PRAGMA foreign_keys = ON";
                            fkOn.ExecuteNonQuery();
                        }
                    }
                    catch (Exception ex)
                    {
                        warnings.Add("weeks: " + ex.Message);
                    }
                }

                if (body.TryGetProperty("catalog", out var catalogEl))
                {
                    try
                    {
                        using var con2 = dbContext.CreateConnection();
                        con2.Open();
                        using var tx2 = con2.BeginTransaction();
                        var categoryIdMap = new Dictionary<int, int>();

                    using var delVar = con2.CreateCommand();
                    delVar.CommandText = @"
                        DELETE FROM product_variations
                        WHERE product_id IN (
                            SELECT p.id FROM products_v2 p
                            JOIN product_categories c ON c.id = p.category_id
                            WHERE c.project_id = @pid
                        )";
                    delVar.Parameters.AddWithValue("@pid", projectId);
                    delVar.ExecuteNonQuery();

                    using var delProd = con2.CreateCommand();
                    delProd.CommandText = @"
                        DELETE FROM products_v2
                        WHERE category_id IN (
                            SELECT id FROM product_categories WHERE project_id = @pid
                        )";
                    delProd.Parameters.AddWithValue("@pid", projectId);
                    delProd.ExecuteNonQuery();

                    using var delAttr = con2.CreateCommand();
                    delAttr.CommandText = "DELETE FROM product_attributes WHERE category_id IN (SELECT id FROM product_categories WHERE project_id = @pid)";
                    delAttr.Parameters.AddWithValue("@pid", projectId);
                    delAttr.ExecuteNonQuery();

                    using var delCat = con2.CreateCommand();
                    delCat.CommandText = "DELETE FROM product_categories WHERE project_id = @pid";
                    delCat.Parameters.AddWithValue("@pid", projectId);
                    delCat.ExecuteNonQuery();

                    if (catalogEl.TryGetProperty("categories", out var catsEl))
                    {
                        var cats = JsonSerializer.Deserialize<List<JsonElement>>(catsEl.GetRawText(), ApiHelpers.JsonOptions);
                        if (cats != null)
                        {
                            foreach (var cat in cats)
                            {
                                var oldCatId = cat.TryGetProperty("id", out var cidEl) && cidEl.ValueKind == JsonValueKind.Number
                                    ? cidEl.GetInt32()
                                    : 0;
                                using var cCmd = con2.CreateCommand();
                                var catName = cat.TryGetProperty("name", out var cn) ? cn.GetString() ?? "" : "";
                                if (string.IsNullOrWhiteSpace(catName)) continue;

                                cCmd.CommandText = "INSERT INTO product_categories (name, description, color, project_id) VALUES (@n, @d, @c, @pid); SELECT last_insert_rowid();";
                                cCmd.Parameters.AddWithValue("@n", catName);
                                cCmd.Parameters.AddWithValue("@d", cat.TryGetProperty("description", out var cd) && cd.ValueKind != JsonValueKind.Null ? cd.GetString() : (object)DBNull.Value);
                                cCmd.Parameters.AddWithValue("@c", cat.TryGetProperty("color", out var cc) ? cc.GetString() ?? "#4f8ef7" : "#4f8ef7");
                                cCmd.Parameters.AddWithValue("@pid", projectId);
                                var newCatId = (long)(cCmd.ExecuteScalar() ?? 0L);
                                if (oldCatId > 0 && newCatId > 0) categoryIdMap[oldCatId] = (int)newCatId;

                                if (cat.TryGetProperty("attributes", out var attrsEl))
                                {
                                    var attrs = JsonSerializer.Deserialize<List<JsonElement>>(attrsEl.GetRawText(), ApiHelpers.JsonOptions);
                                    if (attrs != null)
                                    {
                                        foreach (var attr in attrs)
                                        {
                                            using var aCmd = con2.CreateCommand();
                                            var attrName = attr.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                                            if (string.IsNullOrWhiteSpace(attrName)) continue;
                                            aCmd.CommandText = "INSERT INTO product_attributes (category_id, name, field_type, options, required, sort_order) VALUES (@c, @n, @ft, @o, @r, @s)";
                                            aCmd.Parameters.AddWithValue("@c", (int)newCatId);
                                            aCmd.Parameters.AddWithValue("@n", attrName);
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
                                var prodName = prod.TryGetProperty("name", out var pn) ? pn.GetString() ?? "" : "";
                                if (string.IsNullOrWhiteSpace(prodName)) continue;
                                string attrValuesStr = "{}";
                                if (prod.TryGetProperty("attributeValues", out var av))
                                {
                                    attrValuesStr = av.ValueKind == JsonValueKind.String
                                        ? av.GetString() ?? "{}"
                                        : av.GetRawText();
                                }

                                int? categoryId = null;
                                if (prod.TryGetProperty("categoryId", out var cid) && cid.ValueKind == JsonValueKind.Number)
                                {
                                    var oldCategoryId = cid.GetInt32();
                                    categoryId = categoryIdMap.TryGetValue(oldCategoryId, out var mappedId)
                                        ? mappedId
                                        : oldCategoryId;
                                }
                                if (categoryId == null)
                                {
                                    using var fallbackCatCmd = con2.CreateCommand();
                                    fallbackCatCmd.CommandText = "SELECT id FROM product_categories WHERE project_id = @pid ORDER BY id LIMIT 1";
                                    fallbackCatCmd.Parameters.AddWithValue("@pid", projectId);
                                    var fallbackId = fallbackCatCmd.ExecuteScalar();
                                    if (fallbackId != null) categoryId = Convert.ToInt32(fallbackId);
                                }
                                if (categoryId == null) continue;

                                using var pCmd = con2.CreateCommand();
                                pCmd.CommandText = "INSERT INTO products_v2 (category_id, name, description, attribute_values, created_at) VALUES (@c, @n, @d, @av, @ca); SELECT last_insert_rowid();";
                                pCmd.Parameters.AddWithValue("@c", categoryId.Value);
                                pCmd.Parameters.AddWithValue("@n", prodName);
                                pCmd.Parameters.AddWithValue("@d", prod.TryGetProperty("description", out var pd) && pd.ValueKind != JsonValueKind.Null ? pd.GetString() : (object)DBNull.Value);
                                pCmd.Parameters.AddWithValue("@av", attrValuesStr);
                                pCmd.Parameters.AddWithValue("@ca", prod.TryGetProperty("createdAt", out var pca) && pca.ValueKind == JsonValueKind.String
                                    ? pca.GetString() ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                                    : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                var newProdId = (long)(pCmd.ExecuteScalar() ?? 0L);

                                if (prod.TryGetProperty("variations", out var varsEl))
                                {
                                    var vars = JsonSerializer.Deserialize<List<JsonElement>>(varsEl.GetRawText(), ApiHelpers.JsonOptions);
                                    if (vars != null)
                                    {
                                        foreach (var v in vars)
                                        {
                                            var vName = v.TryGetProperty("name", out var vn) ? vn.GetString() ?? "" : "";
                                            if (string.IsNullOrWhiteSpace(vName)) continue;
                                            using var vCmd = con2.CreateCommand();
                                            vCmd.CommandText = "INSERT OR IGNORE INTO product_variations (product_id, name, sku, price, stock, created_at) VALUES (@p, @n, @s, @pr, @st, @ca)";
                                            vCmd.Parameters.AddWithValue("@p", (int)newProdId);
                                            vCmd.Parameters.AddWithValue("@n", vName);
                                            vCmd.Parameters.AddWithValue("@s", v.TryGetProperty("sku", out var sku) ? sku.GetString() ?? $"{newProdId}-{Guid.NewGuid():N}" : $"{newProdId}-{Guid.NewGuid():N}");
                                            vCmd.Parameters.AddWithValue("@pr", ReadDecimal(v, "price") ?? 0);
                                            vCmd.Parameters.AddWithValue("@st", ReadInt(v, "stock") ?? 0);
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
                    catch (Exception ex)
                    {
                        warnings.Add("catalog: " + ex.Message);
                    }
                }

                return Results.Ok(new { imported = true, warnings });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return app;
    }

    private static ProjectSettings ParseImportSettings(JsonElement settingsEl)
    {
        var settings = new ProjectSettings();

        settings.ProjectName = ReadString(settingsEl, "projectName", "project_name")
            ?? settings.ProjectName;
        settings.StartDate = ReadString(settingsEl, "startDate", "start_date")
            ?? settings.StartDate;
        settings.Description = ReadString(settingsEl, "description")
            ?? settings.Description;
        settings.Currency = ReadString(settingsEl, "currency")
            ?? settings.Currency;
        settings.ProjectImage = ReadString(settingsEl, "projectImage", "project_image");
        settings.VisibleTabs = ReadString(settingsEl, "visibleTabs", "visible_tabs");

        return settings;
    }

    private static string? ReadString(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var val)) continue;
            if (val.ValueKind == JsonValueKind.Null || val.ValueKind == JsonValueKind.Undefined) return null;
            if (val.ValueKind == JsonValueKind.String) return val.GetString();

            // Be tolerant for legacy/variant exports where value might be non-string.
            return val.ToString();
        }

        return null;
    }

    private static int? ReadInt(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var val)) continue;
            if (val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out var i)) return i;
            if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out var fromString)) return fromString;
        }
        return null;
    }

    private static decimal? ReadDecimal(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var val)) continue;
            if (val.ValueKind == JsonValueKind.Number && val.TryGetDecimal(out var d)) return d;
            if (val.ValueKind == JsonValueKind.String && decimal.TryParse(val.GetString(), out var fromString)) return fromString;
        }
        return null;
    }
}
