namespace AuraPrintsApi.Models;

public class CalendarEvent
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Date { get; set; } = "";         // YYYY-MM-DD
    public string? EndDate { get; set; }            // YYYY-MM-DD (optional)
    public string? Time { get; set; }               // HH:MM (optional)
    public string? Description { get; set; }
    public string Color { get; set; } = "#4f8ef7";
    public string Type { get; set; } = "event";    // event | deadline | meeting | delivery
    public string CreatedAt { get; set; } = "";
}
