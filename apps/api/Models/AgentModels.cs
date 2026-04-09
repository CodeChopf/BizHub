using System.Text.Json;

namespace AuraPrintsApi.Models;

public class AgentMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

public class AgentAction
{
    public string Tool { get; set; } = "";
    public Dictionary<string, JsonElement> Params { get; set; } = new();
    public string Summary { get; set; } = "";
}

public class AgentChatRequest
{
    public List<AgentMessage> Messages { get; set; } = new();
    public AgentAction? ConfirmAction { get; set; }
}

public class AgentChatResponse
{
    public string Status { get; set; } = "ok";
    public string Message { get; set; } = "";
    public AgentAction? PendingAction { get; set; }
}

public class AgentUsageSummary
{
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string Tier { get; set; } = "free";
    public long InputTokens30d { get; set; }
    public long OutputTokens30d { get; set; }
    public int LimitInput { get; set; }
    public int LimitOutput { get; set; }
}
