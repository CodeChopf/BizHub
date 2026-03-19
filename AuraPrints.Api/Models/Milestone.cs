namespace AuraPrintsApi.Models;

public class Milestone
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string CreatedAt { get; set; } = "";
    public string Snapshot { get; set; } = "";
}

public class MilestoneListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string CreatedAt { get; set; } = "";
}