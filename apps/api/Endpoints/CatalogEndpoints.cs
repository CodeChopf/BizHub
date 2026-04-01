using System.Text.Json;
using AuraPrintsApi.Repositories;

namespace AuraPrintsApi.Endpoints;

public static class CatalogEndpoints
{
    public static WebApplication MapCatalogEndpoints(this WebApplication app)
    {
        // GET /api/catalog
        app.MapGet("/api/catalog", (HttpRequest req, IProductCatalogRepository repo) =>
            Results.Ok(repo.GetAll(ApiHelpers.GetProjectId(req))));

        // ── KATEGORIEN ──

        app.MapPost("/api/catalog/categories", async (HttpRequest request, IProductCatalogRepository repo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
            var name = body.GetProperty("name").GetString() ?? "";
            var desc = body.TryGetProperty("description", out var d) ? d.GetString() : null;
            var color = body.TryGetProperty("color", out var c) ? c.GetString() ?? "#4f8ef7" : "#4f8ef7";
            return Results.Ok(repo.CreateCategory(ApiHelpers.GetProjectId(request), name, desc, color));
        });

        app.MapPut("/api/catalog/categories/{id}", async (int id, HttpRequest request, IProductCatalogRepository repo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
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
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
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
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
            var categoryId = body.GetProperty("categoryId").GetInt32();
            var name = body.GetProperty("name").GetString() ?? "";
            var desc = body.TryGetProperty("description", out var d) ? d.GetString() : null;
            var attributeValues = body.TryGetProperty("attributeValues", out var av) ? av.GetRawText() : "{}";
            return Results.Ok(repo.CreateProduct(categoryId, name, desc, attributeValues));
        });

        app.MapPut("/api/catalog/products/{id}", async (int id, HttpRequest request, IProductCatalogRepository repo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
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
            var product = repo.GetAll(ApiHelpers.GetProjectId(request)).Products.FirstOrDefault(p => p.Id == id);
            if (product == null) return Results.NotFound();
            var sku = repo.GenerateSku(product.CategoryId, id, variationName);
            return Results.Ok(new { sku });
        });

        app.MapPost("/api/catalog/variations", async (HttpRequest request, IProductCatalogRepository repo) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
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
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, ApiHelpers.JsonOptions);
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

        return app;
    }
}
