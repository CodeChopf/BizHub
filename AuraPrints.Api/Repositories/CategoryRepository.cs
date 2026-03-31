using AuraPrintsApi.Data;
using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly DatabaseContext _context;

    public CategoryRepository(DatabaseContext context)
    {
        _context = context;
    }

    public List<Category> GetAll(int projectId)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT id, name, color FROM categories WHERE project_id = @pid ORDER BY name";
        cmd.Parameters.AddWithValue("@pid", projectId);
        var result = new List<Category>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Category
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Color = reader.GetString(2)
            });
        }
        return result;
    }

    public Category Add(int projectId, string name, string color)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "INSERT INTO categories (project_id, name, color) VALUES (@pid, @n, @c); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@pid", projectId);
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@c", color);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        return new Category { Id = (int)id, Name = name, Color = color };
    }

    public Category Update(int id, string name, string color)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE categories SET name = @n, color = @c WHERE id = @id";
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@c", color);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
        return new Category { Id = id, Name = name, Color = color };
    }

    public void Delete(int id)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM categories WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }
}