namespace AuraPrintsApi.Models;

public class ActivityEntry
{
    public long Id { get; set; }
    public int ProjectId { get; set; }
    public string EntityType { get; set; } = "";
    public string Action { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Actor { get; set; }
    public string CreatedAt { get; set; } = "";
}
