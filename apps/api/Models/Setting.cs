namespace AuraPrintsApi.Models;

public class Setting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public class ProjectSettings
{
    public string ProjectName { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string Description { get; set; } = "";
    public string Currency { get; set; } = "CHF";
    public bool IsSetup { get; set; } = false;
    public string? ProjectImage { get; set; } = null;
    // JSON-String: {"roadmap":true,"produkte":true,...} — null = alle sichtbar
    public string? VisibleTabs { get; set; } = null;
}