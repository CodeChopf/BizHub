using AuraPrintsApi.Data;
using AuraPrintsApi.Models;
using BC = BCrypt.Net.BCrypt;

namespace AuraPrintsApi.Repositories;

public class UserRepository : IUserRepository
{
    private readonly DatabaseContext _context;

    public UserRepository(DatabaseContext context)
    {
        _context = context;
    }

    public bool HasAnyUser()
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM users";
        return (long)(cmd.ExecuteScalar() ?? 0L) > 0;
    }

    public List<User> GetAll()
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT id, username, is_admin, is_platform_admin, created_at FROM users ORDER BY id";
        var list = new List<User>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(new User
            {
                Id              = reader.GetInt32(0),
                Username        = reader.GetString(1),
                IsAdmin         = reader.GetInt32(2) == 1,
                IsPlatformAdmin = reader.GetInt32(3) == 1,
                CreatedAt       = reader.GetString(4)
            });
        return list;
    }

    public User? GetByUsername(string username)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT id, username, password_hash, is_admin, is_platform_admin, created_at FROM users WHERE username = @u";
        cmd.Parameters.AddWithValue("@u", username);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new User
        {
            Id              = reader.GetInt32(0),
            Username        = reader.GetString(1),
            IsAdmin         = reader.GetInt32(3) == 1,
            IsPlatformAdmin = reader.GetInt32(4) == 1,
            CreatedAt       = reader.GetString(5),
            // password_hash exposed via separate property only for auth
            PasswordHash    = reader.GetString(2)
        };
    }

    public User Create(string username, string password, bool isAdmin, bool isPlatformAdmin = false)
        => CreateWithHash(username, BC.HashPassword(password), isAdmin, isPlatformAdmin);

    public User CreateWithHash(string username, string passwordHash, bool isAdmin, bool isPlatformAdmin = false)
    {
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO users (username, password_hash, is_admin, is_platform_admin, created_at)
            VALUES (@u, @h, @a, @pa, @ca);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@u",  username);
        cmd.Parameters.AddWithValue("@h",  passwordHash);
        cmd.Parameters.AddWithValue("@a",  isAdmin ? 1 : 0);
        cmd.Parameters.AddWithValue("@pa", isPlatformAdmin ? 1 : 0);
        cmd.Parameters.AddWithValue("@ca", now);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        return new User { Id = (int)id, Username = username, IsAdmin = isAdmin, IsPlatformAdmin = isPlatformAdmin, CreatedAt = now };
    }

    public void Delete(string username)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM users WHERE username = @u";
        cmd.Parameters.AddWithValue("@u", username);
        cmd.ExecuteNonQuery();
    }

    public void ChangePassword(string username, string newPassword)
    {
        using var con = _context.CreateConnection();
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE users SET password_hash = @h WHERE username = @u";
        cmd.Parameters.AddWithValue("@h", BC.HashPassword(newPassword));
        cmd.Parameters.AddWithValue("@u", username);
        cmd.ExecuteNonQuery();
    }
}
