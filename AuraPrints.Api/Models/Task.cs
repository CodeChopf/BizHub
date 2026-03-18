namespace AuraPrintsApi.Models;

public class AppTask
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string Text { get; set; } = "";
    public string Hours { get; set; } = "";
}