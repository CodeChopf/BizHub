namespace AuraPrintsApi.Models;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? StartDate { get; set; }
    public string Currency { get; set; } = "CHF";
    public string? ProjectImage { get; set; }
    public string? VisibleTabs { get; set; }
    public string CreatedAt { get; set; } = "";
    public string Role { get; set; } = "member"; // role of the requesting user in this project
}
