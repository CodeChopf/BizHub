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

    public List<ProductionItem> GetAll()
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
            ORDER BY pq.done ASC, pq.added_at DESC";

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

    public ProductionItem Add(int productId, int? variationId, int quantity, string? note)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        cmd.CommandText = @"
            INSERT INTO production_queue (product_id, variation_id, quantity, done, note, added_at)
            VALUES (@pid, @vid, @qty, 0, @note, @now);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@pid",  productId);
        cmd.Parameters.AddWithValue("@vid",  (object?)variationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@qty",  quantity);
        cmd.Parameters.AddWithValue("@note", (object?)note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@now",  now);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);

        return GetAll().First(i => i.Id == (int)id);
    }

    public void SetDone(int id, bool done)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE production_queue SET done = @done WHERE id = @id";
        cmd.Parameters.AddWithValue("@done", done ? 1 : 0);
        cmd.Parameters.AddWithValue("@id",   id);
        cmd.ExecuteNonQuery();
    }

    public void UpdateItem(int id, int quantity, string? note)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE production_queue SET quantity = @qty, note = @note WHERE id = @id";
        cmd.Parameters.AddWithValue("@qty",  quantity);
        cmd.Parameters.AddWithValue("@note", (object?)note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id",   id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM production_queue WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteAllDone()
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM production_queue WHERE done = 1";
        cmd.ExecuteNonQuery();
    }
}
