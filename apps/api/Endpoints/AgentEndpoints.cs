using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using AuraPrintsApi.Models;
using AuraPrintsApi.Repositories;

namespace AuraPrintsApi.Endpoints;

public static class AgentEndpoints
{
    private const string ToolGetRoadmap    = "get_roadmap";
    private const string ToolGetCatalog    = "get_catalog";
    private const string ToolCreateWeek    = "create_week";
    private const string ToolUpdateWeek    = "update_week";
    private const string ToolCreateTask    = "create_task";
    private const string ToolUpdateTask    = "update_task";
    private const string ToolCreateProduct = "create_product";
    private const string ToolUpdateProduct = "update_product";

    private static readonly HashSet<string> WriteTools = new()
    {
        ToolCreateWeek, ToolUpdateWeek, ToolCreateTask, ToolUpdateTask,
        ToolCreateProduct, ToolUpdateProduct
    };

    private static readonly List<Anthropic.SDK.Common.Tool> AgentTools = BuildTools();

    public static WebApplication MapAgentEndpoints(this WebApplication app)
    {
        app.MapPost("/api/agent/chat", async (
            HttpRequest req,
            HttpContext ctx,
            IUserRepository userRepo,
            IAgentRepository agentRepo,
            IRoadmapRepository roadmapRepo,
            IStateRepository stateRepo,
            IAdminRepository adminRepo,
            ISettingsRepository settingsRepo,
            IProductCatalogRepository catalogRepo,
            AnthropicClient anthropic) =>
        {
            var username = ctx.User.Identity?.Name ?? "";
            var user = userRepo.GetByUsername(username);
            if (user == null) return Results.Unauthorized();
            if (!ctx.User.IsInRole("admin")) return Results.Forbid();

            var projectId = ApiHelpers.GetProjectId(req);

            // Token limit check
            var tier = agentRepo.GetUserTier(user.Id);
            var (limitIn, limitOut) = agentRepo.GetTierLimits(tier);
            var (usedIn, usedOut) = agentRepo.GetUsage30d(user.Id);
            if (usedIn >= limitIn || usedOut >= limitOut)
            {
                return Results.Json(
                    new AgentChatResponse { Status = "limit_exceeded", Message = "Token-Limit erreicht. Bitte upgraden." },
                    statusCode: 402);
            }

            AgentChatRequest body;
            try
            {
                body = (await JsonSerializer.DeserializeAsync<AgentChatRequest>(req.Body, ApiHelpers.JsonOptions))!;
            }
            catch
            {
                return Results.BadRequest("Invalid request body");
            }

            if (body?.Messages == null || body.Messages.Count == 0)
                return Results.BadRequest("No messages");

            var systemPrompt = BuildSystemPrompt(projectId, roadmapRepo, stateRepo, settingsRepo);

            // Confirmation of a pending write action
            if (body.ConfirmAction != null)
            {
                return await HandleConfirmation(
                    body.ConfirmAction, projectId, user.Id,
                    systemPrompt, agentRepo, adminRepo, catalogRepo, anthropic);
            }

            // Regular chat
            var claudeMessages = BuildClaudeMessages(body.Messages);
            return await CallClaudeWithTools(
                claudeMessages, systemPrompt, projectId, user.Id,
                agentRepo, roadmapRepo, stateRepo, catalogRepo, anthropic);
        });

        app.MapGet("/api/agent/admin/usage", (
            HttpContext ctx,
            IAgentRepository agentRepo) =>
        {
            if (!ctx.User.IsInRole("admin")) return Results.Forbid();
            return Results.Ok(agentRepo.GetAllUsageSummaries());
        });

        app.MapPut("/api/agent/admin/users/{userId}/tier", async (
            int userId,
            HttpRequest req,
            HttpContext ctx,
            IAgentRepository agentRepo) =>
        {
            if (!ctx.User.IsInRole("admin")) return Results.Forbid();
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, ApiHelpers.JsonOptions);
            if (!body.TryGetProperty("tier", out var tierEl)) return Results.BadRequest("tier required");
            var tierVal = tierEl.GetString() ?? "free";
            if (tierVal is not ("free" or "basic" or "pro")) return Results.BadRequest("Invalid tier");
            agentRepo.SetUserTier(userId, tierVal);
            return Results.Ok(new { ok = true });
        });

        return app;
    }

    // ─── System Prompt ────────────────────────────────────────────────────────

    private static string BuildSystemPrompt(
        int projectId,
        IRoadmapRepository roadmapRepo,
        IStateRepository stateRepo,
        ISettingsRepository settingsRepo)
    {
        var settings = settingsRepo.GetSettings(projectId);
        var projectName = string.IsNullOrEmpty(settings.ProjectName) ? "Projekt" : settings.ProjectName;
        var today = DateTime.Now.ToString("yyyy-MM-dd");

        AppData roadmap;
        try { roadmap = roadmapRepo.GetAll(projectId); } catch { roadmap = new AppData(); }

        Dictionary<string, bool> state;
        try { state = stateRepo.GetState(projectId); } catch { state = new Dictionary<string, bool>(); }

        int totalTasks = 0, doneTasks = 0;
        foreach (var week in roadmap.Weeks)
            foreach (var task in week.Tasks)
            {
                totalTasks++;
                if (state.TryGetValue($"task-{task.Id}", out var done) && done) doneTasks++;
            }

        int progressPct = totalTasks > 0 ? (int)(doneTasks * 100.0 / totalTasks) : 0;

        string currentWeekInfo = "keine Wochen vorhanden";
        foreach (var week in roadmap.Weeks)
        {
            int openCount = week.Tasks.Count(t => !(state.TryGetValue($"task-{t.Id}", out var d) && d));
            if (openCount > 0)
            {
                currentWeekInfo = $"Woche {week.Number} — \"{week.Title}\" ({openCount} offene Tasks)";
                break;
            }
        }

        return $"""
            Du bist ein Projektmanagement-Assistent für BizHub. Heute ist {today}.
            Projekt: "{projectName}" (ID: {projectId}).
            Antworte in der Sprache des Benutzers — erkenne sie automatisch und antworte auf Deutsch oder Englisch.

            Aktueller Projektstatus:
            - Fortschritt: {progressPct}% ({doneTasks}/{totalTasks} Tasks erledigt)
            - Aktuelle Woche: {currentWeekInfo}

            Regeln:
            - Für ALLE Schreiboperationen: Beschreibe zuerst genau was du tun wirst und warte auf Bestätigung des Benutzers, BEVOR du ein Write-Tool aufrufst.
            - Niemals Daten löschen.
            - Kein Zugriff auf Finanzdaten oder App-Einstellungen.
            """;
    }

    // ─── Message Conversion ───────────────────────────────────────────────────

    private static List<Message> BuildClaudeMessages(List<AgentMessage> history)
    {
        var result = new List<Message>();
        foreach (var msg in history)
        {
            var role = msg.Role == "assistant" ? RoleType.Assistant : RoleType.User;
            result.Add(new Message(role, msg.Content));
        }
        return result;
    }

    // ─── Main Claude Call Loop ────────────────────────────────────────────────

    private static async Task<IResult> CallClaudeWithTools(
        List<Message> claudeMessages,
        string systemPrompt,
        int projectId,
        int userId,
        IAgentRepository agentRepo,
        IRoadmapRepository roadmapRepo,
        IStateRepository stateRepo,
        IProductCatalogRepository catalogRepo,
        AnthropicClient anthropic)
    {
        int totalIn = 0, totalOut = 0;

        while (true)
        {
            var response = await anthropic.Messages.GetClaudeMessageAsync(new MessageParameters
            {
                Model       = AnthropicModels.Claude46Sonnet,
                MaxTokens   = 2048,
                System      = new List<SystemMessage> { new(systemPrompt) },
                Messages    = claudeMessages,
                Tools       = AgentTools,
                Temperature = 0.7m
            });

            totalIn  += response.Usage?.InputTokens  ?? 0;
            totalOut += response.Usage?.OutputTokens ?? 0;

            if (response.StopReason == "end_turn" || response.StopReason == null)
            {
                agentRepo.RecordUsage(userId, projectId, totalIn, totalOut);
                return Results.Ok(new AgentChatResponse { Status = "ok", Message = ExtractText(response) });
            }

            if (response.StopReason == "tool_use")
            {
                var toolCalls = response.ToolCalls ?? new List<Function>();

                // Write tool → return to frontend for confirmation
                var writeTool = toolCalls.FirstOrDefault(t => WriteTools.Contains(t.Name));
                if (writeTool != null)
                {
                    agentRepo.RecordUsage(userId, projectId, totalIn, totalOut);
                    var action  = BuildAgentAction(writeTool);
                    var preText = ExtractText(response);
                    return Results.Ok(new AgentChatResponse
                    {
                        Status        = "confirmation_required",
                        Message       = string.IsNullOrEmpty(preText) ? action.Summary : preText,
                        PendingAction = action
                    });
                }

                // Read tools → execute immediately and loop
                claudeMessages.Add(response.Message);
                // All tool results must go in a single user message
                var toolResultBlocks = new List<ContentBase>();
                foreach (var tc in toolCalls)
                {
                    var toolRes = ExecuteReadTool(tc, projectId, roadmapRepo, stateRepo, catalogRepo);
                    toolResultBlocks.Add(new ToolResultContent
                    {
                        ToolUseId = tc.Id,
                        Content   = new List<ContentBase> { new TextContent { Text = toolRes } }
                    });
                }
                claudeMessages.Add(new Message { Role = RoleType.User, Content = toolResultBlocks });
                continue;
            }

            // Unexpected stop
            agentRepo.RecordUsage(userId, projectId, totalIn, totalOut);
            return Results.Ok(new AgentChatResponse { Status = "ok", Message = ExtractText(response) });
        }
    }

    // ─── Confirmation Handler ─────────────────────────────────────────────────

    private static async Task<IResult> HandleConfirmation(
        AgentAction action,
        int projectId,
        int userId,
        string systemPrompt,
        IAgentRepository agentRepo,
        IAdminRepository adminRepo,
        IProductCatalogRepository catalogRepo,
        AnthropicClient anthropic)
    {
        string toolResult;
        try
        {
            toolResult = ExecuteWriteTool(action, projectId, adminRepo, catalogRepo);
        }
        catch (Exception ex)
        {
            return Results.Ok(new AgentChatResponse
            {
                Status  = "ok",
                Message = $"Fehler beim Ausführen der Aktion: {ex.Message}"
            });
        }

        // Ask Claude for a natural confirmation message using a fresh mini-conversation
        var confirmMessages = new List<Message>
        {
            new(RoleType.User,
                $"Die folgende Aktion wurde erfolgreich ausgeführt: {action.Summary}. " +
                $"Ergebnis: {toolResult}. " +
                $"Bitte bestätige dem Benutzer kurz und freundlich in seiner Sprache.")
        };

        MessageResponse finalResponse;
        try
        {
            finalResponse = await anthropic.Messages.GetClaudeMessageAsync(new MessageParameters
            {
                Model       = AnthropicModels.Claude46Sonnet,
                MaxTokens   = 512,
                System      = new List<SystemMessage> { new(systemPrompt) },
                Messages    = confirmMessages,
                Temperature = 0.7m
            });
        }
        catch
        {
            agentRepo.RecordUsage(userId, projectId, 0, 0);
            return Results.Ok(new AgentChatResponse { Status = "ok", Message = $"Erledigt: {action.Summary}" });
        }

        agentRepo.RecordUsage(userId, projectId,
            finalResponse.Usage?.InputTokens  ?? 0,
            finalResponse.Usage?.OutputTokens ?? 0);

        return Results.Ok(new AgentChatResponse
        {
            Status  = "ok",
            Message = ExtractText(finalResponse)
        });
    }

    // ─── Tool Execution ───────────────────────────────────────────────────────

    private static string ExecuteReadTool(
        Function toolCall,
        int projectId,
        IRoadmapRepository roadmapRepo,
        IStateRepository stateRepo,
        IProductCatalogRepository catalogRepo)
    {
        if (toolCall.Name == ToolGetRoadmap)
        {
            var data  = roadmapRepo.GetAll(projectId);
            var state = stateRepo.GetState(projectId);
            return JsonSerializer.Serialize(new { weeks = data.Weeks, state }, ApiHelpers.JsonOptions);
        }
        if (toolCall.Name == ToolGetCatalog)
            return JsonSerializer.Serialize(catalogRepo.GetAll(projectId), ApiHelpers.JsonOptions);

        return $"{{\"error\":\"Unknown tool: {toolCall.Name}\"}}";
    }

    private static string ExecuteWriteTool(
        AgentAction action,
        int projectId,
        IAdminRepository adminRepo,
        IProductCatalogRepository catalogRepo)
    {
        var p = action.Params;

        string Str(string key, string def = "") =>
            p.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? def : def;
        int Int(string key, int def = 0) =>
            p.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetInt32() : def;
        string? NullStr(string key) =>
            p.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;

        switch (action.Tool)
        {
            case ToolCreateWeek:
            {
                var week = adminRepo.CreateWeek(projectId, new CreateWeekRequest
                {
                    Title     = Str("title"),
                    Phase     = Str("phase"),
                    BadgePc   = Str("badge_pc"),
                    BadgePhys = Str("badge_phys"),
                    Note      = NullStr("note")
                });
                return JsonSerializer.Serialize(week, ApiHelpers.JsonOptions);
            }
            case ToolUpdateWeek:
            {
                var week = adminRepo.UpdateWeek(projectId, Int("week_number"), new UpdateWeekRequest
                {
                    Title     = Str("title"),
                    Phase     = Str("phase"),
                    BadgePc   = Str("badge_pc"),
                    BadgePhys = Str("badge_phys"),
                    Note      = NullStr("note")
                });
                return JsonSerializer.Serialize(week, ApiHelpers.JsonOptions);
            }
            case ToolCreateTask:
            {
                var task = adminRepo.CreateTask(projectId, new CreateTaskRequest
                {
                    WeekNumber = Int("week_number"),
                    Type       = Str("type"),
                    Text       = Str("text"),
                    Hours      = Str("hours")
                });
                return JsonSerializer.Serialize(task, ApiHelpers.JsonOptions);
            }
            case ToolUpdateTask:
            {
                var task = adminRepo.UpdateTask(Int("task_id"), new UpdateTaskRequest
                {
                    Type  = Str("type"),
                    Text  = Str("text"),
                    Hours = Str("hours")
                });
                return JsonSerializer.Serialize(task, ApiHelpers.JsonOptions);
            }
            case ToolCreateProduct:
            {
                var attrJson = p.TryGetValue("attribute_values", out var av)
                    ? av.GetRawText() : "{}";
                var product = catalogRepo.CreateProduct(
                    Int("category_id"), Str("name"), NullStr("description"), attrJson);
                return JsonSerializer.Serialize(product, ApiHelpers.JsonOptions);
            }
            case ToolUpdateProduct:
            {
                var attrJson = p.TryGetValue("attribute_values", out var av)
                    ? av.GetRawText() : "{}";
                var product = catalogRepo.UpdateProduct(
                    Int("product_id"), Str("name"), NullStr("description"), attrJson);
                return JsonSerializer.Serialize(product, ApiHelpers.JsonOptions);
            }
            default:
                return $"{{\"error\":\"Unknown tool: {action.Tool}\"}}";
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string ExtractText(MessageResponse response)
    {
        if (response.Content == null) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var block in response.Content)
            if (block is TextContent tc && !string.IsNullOrEmpty(tc.Text))
                sb.Append(tc.Text);
        return sb.ToString().Trim();
    }

    private static AgentAction BuildAgentAction(Function toolCall)
    {
        var paramsDict = new Dictionary<string, JsonElement>();
        if (toolCall.Arguments != null)
        {
            try
            {
                var obj = toolCall.Arguments.AsObject();
                foreach (var kvp in obj)
                {
                    if (kvp.Value != null)
                        paramsDict[kvp.Key] = JsonSerializer.Deserialize<JsonElement>(kvp.Value.ToJsonString());
                }
            }
            catch { /* ignore parse errors */ }
        }

        return new AgentAction
        {
            Tool    = toolCall.Name,
            Params  = paramsDict,
            Summary = BuildSummary(toolCall.Name, paramsDict)
        };
    }

    private static string BuildSummary(string tool, Dictionary<string, JsonElement> p)
    {
        string Str(string k) => p.TryGetValue(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        int    Int(string k) => p.TryGetValue(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

        return tool switch
        {
            ToolCreateWeek    => $"Woche erstellen: \"{Str("title")}\" (Phase: {Str("phase")})",
            ToolUpdateWeek    => $"Woche {Int("week_number")} bearbeiten: \"{Str("title")}\"",
            ToolCreateTask    => $"Task erstellen in Woche {Int("week_number")}: \"{Str("text")}\" ({Str("type")}, {Str("hours")})",
            ToolUpdateTask    => $"Task {Int("task_id")} bearbeiten: \"{Str("text")}\"",
            ToolCreateProduct => $"Produkt erstellen: \"{Str("name")}\"",
            ToolUpdateProduct => $"Produkt {Int("product_id")} bearbeiten: \"{Str("name")}\"",
            _                 => tool
        };
    }

    // ─── Tool Definitions ─────────────────────────────────────────────────────

    private static List<Anthropic.SDK.Common.Tool> BuildTools() => new()
    {
        new(new Function(ToolGetRoadmap,
            "Lädt alle Wochen, Tasks und Erledigungsstatus des aktuellen Projekts.",
            """{"type":"object","properties":{}}""")),

        new(new Function(ToolGetCatalog,
            "Lädt den gesamten Produktkatalog (Kategorien und Produkte) des aktuellen Projekts.",
            """{"type":"object","properties":{}}""")),

        new(new Function(ToolCreateWeek,
            "Erstellt eine neue Woche im Projektplan. Nur nach ausdrücklicher Bestätigung des Benutzers aufrufen.",
            """{"type":"object","properties":{"title":{"type":"string"},"phase":{"type":"string"},"badge_pc":{"type":"string"},"badge_phys":{"type":"string"},"note":{"type":"string"}},"required":["title","phase","badge_pc","badge_phys"]}""")),

        new(new Function(ToolUpdateWeek,
            "Bearbeitet eine bestehende Woche. Nur nach ausdrücklicher Bestätigung des Benutzers aufrufen.",
            """{"type":"object","properties":{"week_number":{"type":"integer"},"title":{"type":"string"},"phase":{"type":"string"},"badge_pc":{"type":"string"},"badge_phys":{"type":"string"},"note":{"type":"string"}},"required":["week_number","title","phase","badge_pc","badge_phys"]}""")),

        new(new Function(ToolCreateTask,
            "Erstellt einen neuen Task in einer Woche. Nur nach ausdrücklicher Bestätigung des Benutzers aufrufen.",
            """{"type":"object","properties":{"week_number":{"type":"integer"},"type":{"type":"string","enum":["pc","phys"]},"text":{"type":"string"},"hours":{"type":"string"}},"required":["week_number","type","text","hours"]}""")),

        new(new Function(ToolUpdateTask,
            "Bearbeitet einen bestehenden Task. Nur nach ausdrücklicher Bestätigung des Benutzers aufrufen.",
            """{"type":"object","properties":{"task_id":{"type":"integer"},"type":{"type":"string","enum":["pc","phys"]},"text":{"type":"string"},"hours":{"type":"string"}},"required":["task_id","type","text","hours"]}""")),

        new(new Function(ToolCreateProduct,
            "Erstellt ein neues Produkt im Katalog. Nur nach ausdrücklicher Bestätigung des Benutzers aufrufen.",
            """{"type":"object","properties":{"category_id":{"type":"integer"},"name":{"type":"string"},"description":{"type":"string"},"attribute_values":{"type":"object"}},"required":["category_id","name"]}""")),

        new(new Function(ToolUpdateProduct,
            "Bearbeitet ein bestehendes Produkt. Nur nach ausdrücklicher Bestätigung des Benutzers aufrufen.",
            """{"type":"object","properties":{"product_id":{"type":"integer"},"name":{"type":"string"},"description":{"type":"string"},"attribute_values":{"type":"object"}},"required":["product_id","name"]}""")),
    };
}
