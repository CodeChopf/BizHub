using AuraPrintsApi.Data;
using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly DatabaseContext _context;

    public ProductRepository(DatabaseContext context)
    {
        _context = context;
    }

    public ProductData GetAll()
    {
        using var con = _context.CreateConnection();
        con.Open();

        var products = new List<Product>();
        using var pCmd = con.CreateCommand();
        pCmd.CommandText = "SELECT id, sku, name, type FROM products";
        using var pReader = pCmd.ExecuteReader();
        while (pReader.Read())
        {
            products.Add(new Product
            {
                Sku = pReader.GetString(1),
                Name = pReader.GetString(2),
                Type = pReader.GetString(3)
            });
        }

        using var vCmd = con.CreateCommand();
        vCmd.CommandText = "SELECT product_id, size, height, print_time, price FROM product_variants ORDER BY product_id, id";
        using var vReader = vCmd.ExecuteReader();
        while (vReader.Read())
        {
            var pid = vReader.GetInt32(0);
            products[pid - 1].Variants.Add(new ProductVariant
            {
                Size = vReader.GetString(1),
                Height = vReader.GetString(2),
                PrintTime = vReader.GetString(3),
                Price = vReader.GetString(4)
            });
        }

        var calcs = new List<CostCalc>();
        using var cCmd = con.CreateCommand();
        cCmd.CommandText = "SELECT id, sku, name, sale_price, profit FROM calculations";
        using var cReader = cCmd.ExecuteReader();
        while (cReader.Read())
        {
            calcs.Add(new CostCalc
            {
                Sku = cReader.GetString(1),
                Name = cReader.GetString(2),
                SalePrice = cReader.GetString(3),
                Profit = cReader.GetString(4)
            });
        }

        using var ciCmd = con.CreateCommand();
        ciCmd.CommandText = "SELECT calculation_id, label, amount FROM cost_items ORDER BY calculation_id, sort_order";
        using var ciReader = ciCmd.ExecuteReader();
        while (ciReader.Read())
        {
            var cid = ciReader.GetInt32(0);
            calcs[cid - 1].Costs.Add(new CostItem
            {
                Label = ciReader.GetString(1),
                Amount = ciReader.GetString(2)
            });
        }

        var phase2 = new List<Phase2Item>();
        using var p2Cmd = con.CreateCommand();
        p2Cmd.CommandText = "SELECT label, name, price, note FROM phase2";
        using var p2Reader = p2Cmd.ExecuteReader();
        while (p2Reader.Read())
        {
            phase2.Add(new Phase2Item
            {
                Label = p2Reader.GetString(0),
                Name = p2Reader.GetString(1),
                Price = p2Reader.GetString(2),
                Note = p2Reader.GetString(3)
            });
        }

        var legal = new List<LegalItem>();
        using var lCmd = con.CreateCommand();
        lCmd.CommandText = "SELECT text FROM legal";
        using var lReader = lCmd.ExecuteReader();
        while (lReader.Read())
            legal.Add(new LegalItem { Text = lReader.GetString(0) });

        return new ProductData
        {
            Products = products,
            Calculations = calcs,
            Phase2 = phase2,
            Legal = legal
        };
    }
}