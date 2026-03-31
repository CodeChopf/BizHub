namespace AuraPrintsApi.Models;

public class ProjectMember
{
    public int ProjectId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string Role { get; set; } = "member";
}
