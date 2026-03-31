namespace AuraPrintsApi.Models;

public class Week
{
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string Phase { get; set; } = "";
    public string BadgePc { get; set; } = "";
    public string BadgePhys { get; set; } = "";
    public string? Note { get; set; }
    public List<AppTask> Tasks { get; set; } = new();
}