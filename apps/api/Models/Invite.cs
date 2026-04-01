namespace AuraPrintsApi.Models;

public class Invite
{
    public string Token { get; set; } = "";
    public string Type { get; set; } = "project"; // "platform" | "project"
    public int? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string Role { get; set; } = "member";
    public int CreatedBy { get; set; }
    public string CreatedAt { get; set; } = "";
    public string ExpiresAt { get; set; } = "";
    public string? UsedAt { get; set; }
}
