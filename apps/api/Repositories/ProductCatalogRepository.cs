using AuraPrintsApi.Data;
using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public class ProductCatalogRepository : IProductCatalogRepository
{
    private readonly DatabaseContext _context;

    public ProductCatalogRepository(DatabaseContext context)
    {
        _context = context;
    }

    public ProductCatalogData GetAll(int projectId)
    {
        using var con = _context.CreateConnection();
        con.Open();

        // Kategorien
        var categories = new List<ProductCategory>();
        using var cCmd = con.CreateCommand();
        cCmd.CommandText = "SELECT id, name, description, color FROM product_categories WHERE project_id = @pid ORDER BY id";
        cCmd.Parameters.AddWithValue("@pid", projectId);
        using var cReader = cCmd.ExecuteReader();
        while (cReader.Read())
        {
            categories.Add(new ProductCategory
            {
                Id = cReader.GetInt32(0),
                Name = cReader.GetString(1),
                Description = cReader.IsDBNull(2) ? null : cReader.GetString(2),
                Color = cReader.GetString(3)
            });
        }

        // Attribute
        using var aCmd = con.CreateCommand();
        aCmd.CommandText = "SELECT id, category_id, name, field_type, options, required, sort_order FROM product_attributes ORDER BY category_id, sort_order";
        using var aReader = aCmd.ExecuteReader();
        while (aReader.Read())
        {
            var catId = aReader.GetInt32(1);
            var cat = categories.FirstOrDefault(c => c.Id == catId);
            if (cat == null) continue;
            cat.Attributes.Add(new ProductAttribute
            {
                Id = aReader.GetInt32(0),
                CategoryId = catId,
                Name = aReader.GetString(2),
                FieldType = aReader.GetString(3),
                Options = aReader.IsDBNull(4) ? null : aReader.GetString(4),
                Required = aReader.GetInt32(5) == 1,
                SortOrder = aReader.GetInt32(6)
            });
        }

        // Produkte
        var products = new List<ProductV2>();
        using var pCmd = con.CreateCommand();
        pCmd.CommandText = @"
            SELECT p.id, p.category_id, pc.name, pc.color, p.name, p.description, p.attribute_values, p.created_at
            FROM products_v2 p
            JOIN product_categories pc ON pc.id = p.category_id
            WHERE pc.project_id = @pid
            ORDER BY p.category_id, p.id";
        pCmd.Parameters.AddWithValue("@pid", projectId);
        using var pReader = pCmd.ExecuteReader();
        while (pReader.Read())
        {
            products.Add(new ProductV2
            {
                Id = pReader.GetInt32(0),
                CategoryId = pReader.GetInt32(1),
                CategoryName = pReader.GetString(2),
                CategoryColor = pReader.GetString(3),
                Name = pReader.GetString(4),
                Description = pReader.IsDBNull(5) ? null : pReader.GetString(5),
                AttributeValues = pReader.GetString(6),
                CreatedAt = pReader.GetString(7)
            });
        }

        // Variationen
        using var vCmd = con.CreateCommand();
        vCmd.CommandText = "SELECT id, product_id, name, sku, price, stock, created_at FROM product_variations ORDER BY product_id, id";
        using var vReader = vCmd.ExecuteReader();
        while (vReader.Read())
        {
            var productId = vReader.GetInt32(1);
            var product = products.FirstOrDefault(p => p.Id == productId);
            if (product == null) continue;
            product.Variations.Add(new ProductVariation
            {
                Id = vReader.GetInt32(0),
                ProductId = productId,
                Name = vReader.GetString(2),
                Sku = vReader.GetString(3),
                Price = vReader.GetDecimal(4),
                Stock = vReader.GetInt32(5),
                CreatedAt = vReader.GetString(6)
            });
        }

        return new ProductCatalogData { Categories = categories, Products = products };
    }

    // ── KATEGORIEN ──
    public ProductCategory CreateCategory(int projectId, string name, string? description, string color)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO product_categories (project_id, name, description, color) VALUES (@pid, @n, @d, @c);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@pid", projectId);
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@d", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@c", color);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        return new ProductCategory { Id = (int)id, Name = name, Description = description, Color = color };
    }

    public ProductCategory UpdateCategory(int projectId, int id, string name, string? description, string color)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE product_categories SET name = @n, description = @d, color = @c WHERE id = @id AND project_id = @pid";
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@d", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@c", color);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@pid", projectId);
        cmd.ExecuteNonQuery();
        return new ProductCategory { Id = id, Name = name, Description = description, Color = color };
    }

    public void DeleteCategory(int projectId, int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var tx = con.BeginTransaction();

        // Variationen der Produkte in dieser Kategorie löschen
        using var delVar = con.CreateCommand();
        delVar.CommandText = "DELETE FROM product_variations WHERE product_id IN (SELECT p.id FROM products_v2 p JOIN product_categories c ON c.id = p.category_id WHERE p.category_id = @id AND c.project_id = @pid)";
        delVar.Parameters.AddWithValue("@id", id);
        delVar.Parameters.AddWithValue("@pid", projectId);
        delVar.ExecuteNonQuery();

        using var delAttr = con.CreateCommand();
        delAttr.CommandText = "DELETE FROM product_attributes WHERE category_id = @id AND category_id IN (SELECT id FROM product_categories WHERE project_id = @pid)";
        delAttr.Parameters.AddWithValue("@id", id);
        delAttr.Parameters.AddWithValue("@pid", projectId);
        delAttr.ExecuteNonQuery();

        using var delProd = con.CreateCommand();
        delProd.CommandText = "DELETE FROM products_v2 WHERE category_id = @id AND category_id IN (SELECT id FROM product_categories WHERE project_id = @pid)";
        delProd.Parameters.AddWithValue("@id", id);
        delProd.Parameters.AddWithValue("@pid", projectId);
        delProd.ExecuteNonQuery();

        using var delCat = con.CreateCommand();
        delCat.CommandText = "DELETE FROM product_categories WHERE id = @id AND project_id = @pid";
        delCat.Parameters.AddWithValue("@id", id);
        delCat.Parameters.AddWithValue("@pid", projectId);
        delCat.ExecuteNonQuery();

        tx.Commit();
    }

    // ── ATTRIBUTE ──
    public ProductAttribute AddAttribute(int projectId, int categoryId, string name, string fieldType, string? options, bool required, int sortOrder)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO product_attributes (category_id, name, field_type, options, required, sort_order)
            SELECT @c, @n, @ft, @o, @r, @s
            WHERE EXISTS (SELECT 1 FROM product_categories WHERE id = @c AND project_id = @pid);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@c", categoryId);
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@ft", fieldType);
        cmd.Parameters.AddWithValue("@o", (object?)options ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@r", required ? 1 : 0);
        cmd.Parameters.AddWithValue("@s", sortOrder);
        cmd.Parameters.AddWithValue("@pid", projectId);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        return new ProductAttribute { Id = (int)id, CategoryId = categoryId, Name = name, FieldType = fieldType, Options = options, Required = required, SortOrder = sortOrder };
    }

    public void DeleteAttribute(int projectId, int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM product_attributes WHERE id = @id AND category_id IN (SELECT id FROM product_categories WHERE project_id = @pid)";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@pid", projectId);
        cmd.ExecuteNonQuery();
    }

    // ── PRODUKTE ──
    public ProductV2 CreateProduct(int projectId, int categoryId, string name, string? description, string attributeValues)
    {
        using var con = _context.CreateConnection();
        con.Open();
        var createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO products_v2 (category_id, name, description, attribute_values, created_at)
            SELECT @c, @n, @d, @av, @ca
            WHERE EXISTS (SELECT 1 FROM product_categories WHERE id = @c AND project_id = @pid);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@c", categoryId);
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@d", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@av", attributeValues);
        cmd.Parameters.AddWithValue("@ca", createdAt);
        cmd.Parameters.AddWithValue("@pid", projectId);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);

        using var rCmd = con.CreateCommand();
        rCmd.CommandText = @"
            SELECT p.id, p.category_id, pc.name, pc.color, p.name, p.description, p.attribute_values, p.created_at
            FROM products_v2 p JOIN product_categories pc ON pc.id = p.category_id WHERE p.id = @id AND pc.project_id = @pid";
        rCmd.Parameters.AddWithValue("@id", id);
        rCmd.Parameters.AddWithValue("@pid", projectId);
        using var r = rCmd.ExecuteReader();
        r.Read();
        return new ProductV2
        {
            Id = r.GetInt32(0),
            CategoryId = r.GetInt32(1),
            CategoryName = r.GetString(2),
            CategoryColor = r.GetString(3),
            Name = r.GetString(4),
            Description = r.IsDBNull(5) ? null : r.GetString(5),
            AttributeValues = r.GetString(6),
            CreatedAt = r.GetString(7)
        };
    }

    public ProductV2 UpdateProduct(int projectId, int id, string name, string? description, string attributeValues)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE products_v2 SET name = @n, description = @d, attribute_values = @av WHERE id = @id AND category_id IN (SELECT id FROM product_categories WHERE project_id = @pid)";
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@d", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@av", attributeValues);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@pid", projectId);
        cmd.ExecuteNonQuery();

        using var rCmd = con.CreateCommand();
        rCmd.CommandText = @"
            SELECT p.id, p.category_id, pc.name, pc.color, p.name, p.description, p.attribute_values, p.created_at
            FROM products_v2 p JOIN product_categories pc ON pc.id = p.category_id WHERE p.id = @id AND pc.project_id = @pid";
        rCmd.Parameters.AddWithValue("@id", id);
        rCmd.Parameters.AddWithValue("@pid", projectId);
        using var r = rCmd.ExecuteReader();
        r.Read();
        return new ProductV2
        {
            Id = r.GetInt32(0),
            CategoryId = r.GetInt32(1),
            CategoryName = r.GetString(2),
            CategoryColor = r.GetString(3),
            Name = r.GetString(4),
            Description = r.IsDBNull(5) ? null : r.GetString(5),
            AttributeValues = r.GetString(6),
            CreatedAt = r.GetString(7)
        };
    }

    public void DeleteProduct(int projectId, int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var tx = con.BeginTransaction();
        using var delVar = con.CreateCommand();
        delVar.CommandText = "DELETE FROM product_variations WHERE product_id = @id AND product_id IN (SELECT p.id FROM products_v2 p JOIN product_categories c ON c.id = p.category_id WHERE c.project_id = @pid)";
        delVar.Parameters.AddWithValue("@id", id);
        delVar.Parameters.AddWithValue("@pid", projectId);
        delVar.ExecuteNonQuery();
        using var delProd = con.CreateCommand();
        delProd.CommandText = "DELETE FROM products_v2 WHERE id = @id AND category_id IN (SELECT id FROM product_categories WHERE project_id = @pid)";
        delProd.Parameters.AddWithValue("@id", id);
        delProd.Parameters.AddWithValue("@pid", projectId);
        delProd.ExecuteNonQuery();
        tx.Commit();
    }

    // ── VARIATIONEN ──
    public ProductVariation AddVariation(int projectId, int productId, string name, string sku, decimal price, int stock)
    {
        using var con = _context.CreateConnection();
        con.Open();
        var createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO product_variations (product_id, name, sku, price, stock, created_at)
            SELECT @p, @n, @s, @pr, @st, @ca
            WHERE EXISTS (
                SELECT 1 FROM products_v2 p
                JOIN product_categories c ON c.id = p.category_id
                WHERE p.id = @p AND c.project_id = @pid
            );
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@p", productId);
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@s", sku);
        cmd.Parameters.AddWithValue("@pr", price);
        cmd.Parameters.AddWithValue("@st", stock);
        cmd.Parameters.AddWithValue("@ca", createdAt);
        cmd.Parameters.AddWithValue("@pid", projectId);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        return new ProductVariation { Id = (int)id, ProductId = productId, Name = name, Sku = sku, Price = price, Stock = stock, CreatedAt = createdAt };
    }

    public ProductVariation UpdateVariation(int projectId, int id, string name, string sku, decimal price, int stock)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            UPDATE product_variations
            SET name = @n, sku = @s, price = @pr, stock = @st
            WHERE id = @id AND product_id IN (
                SELECT p.id FROM products_v2 p
                JOIN product_categories c ON c.id = p.category_id
                WHERE c.project_id = @pid
            )";
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@s", sku);
        cmd.Parameters.AddWithValue("@pr", price);
        cmd.Parameters.AddWithValue("@st", stock);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@pid", projectId);
        cmd.ExecuteNonQuery();

        using var rCmd = con.CreateCommand();
        rCmd.CommandText = @"
            SELECT v.id, v.product_id, v.name, v.sku, v.price, v.stock, v.created_at
            FROM product_variations v
            JOIN products_v2 p ON p.id = v.product_id
            JOIN product_categories c ON c.id = p.category_id
            WHERE v.id = @id AND c.project_id = @pid";
        rCmd.Parameters.AddWithValue("@id", id);
        rCmd.Parameters.AddWithValue("@pid", projectId);
        using var r = rCmd.ExecuteReader();
        r.Read();
        return new ProductVariation { Id = r.GetInt32(0), ProductId = r.GetInt32(1), Name = r.GetString(2), Sku = r.GetString(3), Price = r.GetDecimal(4), Stock = r.GetInt32(5), CreatedAt = r.GetString(6) };
    }

    public void DeleteVariation(int projectId, int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM product_variations
            WHERE id = @id AND product_id IN (
                SELECT p.id FROM products_v2 p
                JOIN product_categories c ON c.id = p.category_id
                WHERE c.project_id = @pid
            )";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@pid", projectId);
        cmd.ExecuteNonQuery();
    }

    public bool SkuExists(int projectId, string sku, int? excludeId = null)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        if (excludeId.HasValue)
        {
            cmd.CommandText = @"
                SELECT COUNT(*) FROM product_variations v
                JOIN products_v2 p ON p.id = v.product_id
                JOIN product_categories c ON c.id = p.category_id
                WHERE v.sku = @s AND v.id != @id AND c.project_id = @pid";
            cmd.Parameters.AddWithValue("@id", excludeId.Value);
        }
        else
        {
            cmd.CommandText = @"
                SELECT COUNT(*) FROM product_variations v
                JOIN products_v2 p ON p.id = v.product_id
                JOIN product_categories c ON c.id = p.category_id
                WHERE v.sku = @s AND c.project_id = @pid";
        }
        cmd.Parameters.AddWithValue("@s", sku);
        cmd.Parameters.AddWithValue("@pid", projectId);
        return (long)(cmd.ExecuteScalar() ?? 0L) > 0;
    }

    public string GenerateSku(int projectId, int categoryId, int productId, string variationName)
    {
        using var con = _context.CreateConnection();
        con.Open();

        // Kategorie-Kürzel
        using var catCmd = con.CreateCommand();
        catCmd.CommandText = "SELECT name FROM product_categories WHERE id = @id AND project_id = @pid";
        catCmd.Parameters.AddWithValue("@id", categoryId);
        catCmd.Parameters.AddWithValue("@pid", projectId);
        var catName = catCmd.ExecuteScalar()?.ToString() ?? "XX";
        var catCode = GenerateCode(catName, 3);

        // Produkt-Nummer (fortlaufend pro Kategorie)
        using var numCmd = con.CreateCommand();
        numCmd.CommandText = "SELECT COUNT(*) FROM products_v2 WHERE category_id = @id";
        numCmd.Parameters.AddWithValue("@id", categoryId);
        var prodCount = (long)(numCmd.ExecuteScalar() ?? 0L);
        var prodNum = (prodCount).ToString("D3");

        // Variations-Kürzel
        var varCode = GenerateCode(variationName, 3);

        // Basis-SKU
        var baseSku = string.IsNullOrEmpty(varCode)
            ? $"{catCode}-{prodNum}"
            : $"{catCode}-{prodNum}-{varCode}";

        // Einzigartigkeit sicherstellen
        var sku = baseSku;
        var counter = 1;
        while (SkuExists(projectId, sku))
        {
            sku = $"{baseSku}-{counter++}";
        }

        return sku;
    }

    private static string GenerateCode(string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(name)) return "XX";
        // Grossbuchstaben aus dem Namen extrahieren
        var upper = new string(name.Where(char.IsLetter).Take(maxLength).ToArray()).ToUpper();
        return string.IsNullOrEmpty(upper) ? "XX" : upper;
    }
}