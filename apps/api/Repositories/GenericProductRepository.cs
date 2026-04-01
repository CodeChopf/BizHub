using AuraPrintsApi.Data;
using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public class GenericProductRepository : IGenericProductRepository
{
    private readonly DatabaseContext _context;

    public GenericProductRepository(DatabaseContext context)
    {
        _context = context;
    }

    public ProductData2 GetAll()
    {
        using var con = _context.CreateConnection();
        con.Open();

        // Produkttypen laden
        var types = new List<ProductType>();
        using var tCmd = con.CreateCommand();
        tCmd.CommandText = "SELECT id, name, description, color FROM product_types ORDER BY id";
        using var tReader = tCmd.ExecuteReader();
        while (tReader.Read())
        {
            types.Add(new ProductType
            {
                Id = tReader.GetInt32(0),
                Name = tReader.GetString(1),
                Description = tReader.IsDBNull(2) ? null : tReader.GetString(2),
                Color = tReader.GetString(3)
            });
        }

        // Felder laden
        using var fCmd = con.CreateCommand();
        fCmd.CommandText = "SELECT id, product_type_id, name, field_type, options, required, sort_order FROM product_fields ORDER BY product_type_id, sort_order";
        using var fReader = fCmd.ExecuteReader();
        while (fReader.Read())
        {
            var typeId = fReader.GetInt32(1);
            var type = types.FirstOrDefault(t => t.Id == typeId);
            if (type == null) continue;
            type.Fields.Add(new ProductField
            {
                Id = fReader.GetInt32(0),
                ProductTypeId = typeId,
                Name = fReader.GetString(2),
                FieldType = fReader.GetString(3),
                Options = fReader.IsDBNull(4) ? null : fReader.GetString(4),
                Required = fReader.GetInt32(5) == 1,
                SortOrder = fReader.GetInt32(6)
            });
        }

        // Produkte laden
        var products = new List<GenericProduct>();
        using var pCmd = con.CreateCommand();
        pCmd.CommandText = @"
            SELECT p.id, p.product_type_id, pt.name, pt.color, p.field_values, p.created_at
            FROM products_generic p
            JOIN product_types pt ON pt.id = p.product_type_id
            ORDER BY p.id DESC";
        using var pReader = pCmd.ExecuteReader();
        while (pReader.Read())
        {
            products.Add(new GenericProduct
            {
                Id = pReader.GetInt32(0),
                ProductTypeId = pReader.GetInt32(1),
                ProductTypeName = pReader.GetString(2),
                ProductTypeColor = pReader.GetString(3),
                FieldValues = pReader.GetString(4),
                CreatedAt = pReader.GetString(5)
            });
        }

        return new ProductData2 { ProductTypes = types, Products = products };
    }

    public ProductType CreateProductType(string name, string? description, string color)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO product_types (name, description, color) VALUES (@n, @d, @c);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@d", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@c", color);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        return new ProductType { Id = (int)id, Name = name, Description = description, Color = color };
    }

    public ProductType UpdateProductType(int id, string name, string? description, string color)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE product_types SET name = @n, description = @d, color = @c WHERE id = @id";
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@d", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@c", color);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
        return new ProductType { Id = id, Name = name, Description = description, Color = color };
    }

    public void DeleteProductType(int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var tx = con.BeginTransaction();
        using var delFields = con.CreateCommand();
        delFields.CommandText = "DELETE FROM product_fields WHERE product_type_id = @id";
        delFields.Parameters.AddWithValue("@id", id);
        delFields.ExecuteNonQuery();
        using var delProducts = con.CreateCommand();
        delProducts.CommandText = "DELETE FROM products_generic WHERE product_type_id = @id";
        delProducts.Parameters.AddWithValue("@id", id);
        delProducts.ExecuteNonQuery();
        using var delType = con.CreateCommand();
        delType.CommandText = "DELETE FROM product_types WHERE id = @id";
        delType.Parameters.AddWithValue("@id", id);
        delType.ExecuteNonQuery();
        tx.Commit();
    }

    public ProductField AddField(int productTypeId, string name, string fieldType, string? options, bool required, int sortOrder)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO product_fields (product_type_id, name, field_type, options, required, sort_order)
            VALUES (@pt, @n, @ft, @o, @r, @s);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@pt", productTypeId);
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@ft", fieldType);
        cmd.Parameters.AddWithValue("@o", (object?)options ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@r", required ? 1 : 0);
        cmd.Parameters.AddWithValue("@s", sortOrder);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        return new ProductField { Id = (int)id, ProductTypeId = productTypeId, Name = name, FieldType = fieldType, Options = options, Required = required, SortOrder = sortOrder };
    }

    public void UpdateField(int id, string name, string fieldType, string? options, bool required)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE product_fields SET name = @n, field_type = @ft, options = @o, required = @r WHERE id = @id";
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@ft", fieldType);
        cmd.Parameters.AddWithValue("@o", (object?)options ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@r", required ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteField(int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM product_fields WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public GenericProduct CreateProduct(int productTypeId, string fieldValues)
    {
        using var con = _context.CreateConnection();
        con.Open();
        var createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO products_generic (product_type_id, field_values, created_at)
            VALUES (@pt, @fv, @ca);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@pt", productTypeId);
        cmd.Parameters.AddWithValue("@fv", fieldValues);
        cmd.Parameters.AddWithValue("@ca", createdAt);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);

        using var rCmd = con.CreateCommand();
        rCmd.CommandText = @"
            SELECT p.id, p.product_type_id, pt.name, pt.color, p.field_values, p.created_at
            FROM products_generic p
            JOIN product_types pt ON pt.id = p.product_type_id
            WHERE p.id = @id";
        rCmd.Parameters.AddWithValue("@id", id);
        using var r = rCmd.ExecuteReader();
        r.Read();
        return new GenericProduct
        {
            Id = r.GetInt32(0),
            ProductTypeId = r.GetInt32(1),
            ProductTypeName = r.GetString(2),
            ProductTypeColor = r.GetString(3),
            FieldValues = r.GetString(4),
            CreatedAt = r.GetString(5)
        };
    }

    public GenericProduct UpdateProduct(int id, string fieldValues)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE products_generic SET field_values = @fv WHERE id = @id";
        cmd.Parameters.AddWithValue("@fv", fieldValues);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();

        using var rCmd = con.CreateCommand();
        rCmd.CommandText = @"
            SELECT p.id, p.product_type_id, pt.name, pt.color, p.field_values, p.created_at
            FROM products_generic p
            JOIN product_types pt ON pt.id = p.product_type_id
            WHERE p.id = @id";
        rCmd.Parameters.AddWithValue("@id", id);
        using var r = rCmd.ExecuteReader();
        r.Read();
        return new GenericProduct
        {
            Id = r.GetInt32(0),
            ProductTypeId = r.GetInt32(1),
            ProductTypeName = r.GetString(2),
            ProductTypeColor = r.GetString(3),
            FieldValues = r.GetString(4),
            CreatedAt = r.GetString(5)
        };
    }

    public void DeleteProduct(int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM products_generic WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }
}