namespace AuraPrintsApi.Models;

public class UpdateWeekRequest
{
    public string Title { get; set; } = "";
    public string Phase { get; set; } = "";
    public string BadgePc { get; set; } = "";
    public string BadgePhys { get; set; } = "";
    public string? Note { get; set; }
}

public class CreateWeekRequest
{
    public string Title { get; set; } = "";
    public string Phase { get; set; } = "";
    public string BadgePc { get; set; } = "";
    public string BadgePhys { get; set; } = "";
    public string? Note { get; set; }
}

public class UpdateTaskRequest
{
    public string Type { get; set; } = "";
    public string Text { get; set; } = "";
    public string Hours { get; set; } = "";
    public List<int> TagIds { get; set; } = new();
}

public class CreateTaskRequest
{
    public int WeekNumber { get; set; }
    public string Type { get; set; } = "";
    public string Text { get; set; } = "";
    public string Hours { get; set; } = "";
    public List<int> TagIds { get; set; } = new();
}

public class CreateTaskTagRequest
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#4f8ef7";
}

public class UpdateTaskTagRequest
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#4f8ef7";
}

public class ReorderTasksRequest
{
    public List<int> TaskIds { get; set; } = new();
}

public class CreateSubtaskRequest
{
    public int TaskId { get; set; }
    public string Text { get; set; } = "";
    public string Hours { get; set; } = "";
}

public class UpdateSubtaskRequest
{
    public string Text { get; set; } = "";
    public string Hours { get; set; } = "";
}