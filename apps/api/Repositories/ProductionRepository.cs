using AuraPrintsApi.Data;
using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public class ProductionRepository : IProductionRepository
{
    private readonly DatabaseContext _context;

    public ProductionRepository(DatabaseContext context)
    {
        _context = context;
    }

    public List<ProductionItem> GetAll(int projectId)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            SELECT
                pq.id, pq.product_id, pq.variation_id, pq.quantity, pq.done, pq.note, pq.added_at,
                p.name AS product_name,
                pc.name AS category_name,
                pc.color AS category_color,
                pv.name AS variation_name,
                pv.sku  AS variation_sku
            FROM production_queue pq
            JOIN products_v2 p ON p.id = pq.product_id
            JOIN product_categories pc ON pc.id = p.category_id
            LEFT JOIN product_variations pv ON pv.id = pq.variation_id
            WHERE pq.project_id = @pid
            ORDER BY pq.done ASC, pq.added_at DESC";
        cmd.Parameters.AddWithValue("@pid", projectId);

        var items = new List<ProductionItem>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new ProductionItem
            {
                Id           = reader.GetInt32(0),
                ProductId    = reader.GetInt32(1),
                VariationId  = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                Quantity     = reader.GetInt32(3),
                Done         = reader.GetInt32(4) == 1,
                Note         = reader.IsDBNull(5) ? null : reader.GetString(5),
                AddedAt      = reader.GetString(6),
                ProductName  = reader.GetString(7),
                CategoryName = reader.GetString(8),
                CategoryColor= reader.GetString(9),
                VariationName= reader.IsDBNull(10) ? null : reader.GetString(10),
                VariationSku = reader.IsDBNull(11) ? null : reader.GetString(11),
            });
        }
        return items;
    }

    public ProductionItem Add(int projectId, int productId, int? variationId, int quantity, string? note)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using (var productCheck = con.CreateCommand())
        {
            productCheck.CommandText = @"
                SELECT COUNT(*) FROM products_v2 p
                JOIN product_categories c ON c.id = p.category_id
                WHERE p.id = @productId AND c.project_id = @pid";
            productCheck.Parameters.AddWithValue("@productId", productId);
            productCheck.Parameters.AddWithValue("@pid", projectId);
            if ((long)(productCheck.ExecuteScalar() ?? 0L) == 0L)
                throw new InvalidOperationException("Product not found in current project.");
        }
        if (variationId.HasValue)
        {
            using var variationCheck = con.CreateCommand();
            variationCheck.CommandText = @"
                SELECT COUNT(*) FROM product_variations v
                JOIN products_v2 p ON p.id = v.product_id
                JOIN product_categories c ON c.id = p.category_id
                WHERE v.id = @variationId AND c.project_id = @pid";
            variationCheck.Parameters.AddWithValue("@variationId", variationId.Value);
            variationCheck.Parameters.AddWithValue("@pid", projectId);
            if ((long)(variationCheck.ExecuteScalar() ?? 0L) == 0L)
                throw new InvalidOperationException("Variation not found in current project.");
        }
        using var cmd = con.CreateCommand();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        cmd.CommandText = @"
            INSERT INTO production_queue (project_id, product_id, variation_id, quantity, done, note, added_at)
            VALUES (@projId, @pid, @vid, @qty, 0, @note, @now);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@projId", projectId);
        cmd.Parameters.AddWithValue("@pid",    productId);
        cmd.Parameters.AddWithValue("@vid",    (object?)variationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@qty",    quantity);
        cmd.Parameters.AddWithValue("@note",   (object?)note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@now",    now);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);

        return GetAll(projectId).First(i => i.Id == (int)id);
    }

    public void SetDone(int projectId, int id, bool done)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE production_queue SET done = @done WHERE id = @id AND project_id = @pid";
        cmd.Parameters.AddWithValue("@done", done ? 1 : 0);
        cmd.Parameters.AddWithValue("@id",   id);
        cmd.Parameters.AddWithValue("@pid",  projectId);
        cmd.ExecuteNonQuery();
    }

    public void UpdateItem(int projectId, int id, int quantity, string? note)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE production_queue SET quantity = @qty, note = @note WHERE id = @id AND project_id = @pid";
        cmd.Parameters.AddWithValue("@qty",  quantity);
        cmd.Parameters.AddWithValue("@note", (object?)note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id",   id);
        cmd.Parameters.AddWithValue("@pid",  projectId);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int projectId, int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM production_queue WHERE id = @id AND project_id = @pid";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@pid", projectId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteAllDone(int projectId)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM production_queue WHERE done = 1 AND project_id = @pid";
        cmd.Parameters.AddWithValue("@pid", projectId);
        cmd.ExecuteNonQuery();
    }
}
