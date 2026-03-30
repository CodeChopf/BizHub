namespace AuraPrintsApi.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public bool IsAdmin { get; set; }
    public string CreatedAt { get; set; } = "";
    // Only populated for auth checks — never serialised to API responses
    public string? PasswordHash { get; set; }
}
