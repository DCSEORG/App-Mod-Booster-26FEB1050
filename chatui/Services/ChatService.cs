using Azure.AI.OpenAI;
using Azure.Identity;
using System.Text.Json;

namespace ExpenseChat.Services;

public interface IChatService
{
    Task<string> ChatAsync(string userMessage, List<ChatMessage> history);
}

public record ChatMessage(string Role, string Content);

public class ChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly HttpClient _httpClient;

    public ChatService(IConfiguration configuration, ILogger<ChatService> logger, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("ExpenseApi");
    }

    public async Task<string> ChatAsync(string userMessage, List<ChatMessage> history)
    {
        var endpoint = _configuration["GenAISettings:Endpoint"];
        var deploymentName = _configuration["GenAISettings:DeploymentName"] ?? "gpt-4o";

        if (string.IsNullOrEmpty(endpoint))
        {
            return "**Azure OpenAI is not configured.** The GenAI services have not been deployed yet.\n\n" +
                   "To enable the AI assistant:\n" +
                   "1. Run `bash deploy-with-chat.sh` to deploy Azure OpenAI and AI Search resources\n" +
                   "2. The deployment script will automatically configure the required environment variables\n" +
                   "3. Restart the application after deployment\n\n" +
                   "In the meantime, you can use the **Expense Management** app directly at /Index to view and manage expenses.";
        }

        try
        {
            // Use ManagedIdentityCredential with explicit client ID if provided
            var managedIdentityClientId = _configuration["ManagedIdentityClientId"];
            Azure.Core.TokenCredential credential;

            if (!string.IsNullOrEmpty(managedIdentityClientId))
            {
                _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                credential = new ManagedIdentityCredential(managedIdentityClientId);
            }
            else
            {
                _logger.LogInformation("Using DefaultAzureCredential");
                credential = new DefaultAzureCredential();
            }

            var openAIClient = new OpenAIClient(new Uri(endpoint), credential);

            // Build function tool definitions
            var tools = BuildFunctionToolDefinitions();

            // Build chat messages
            var chatOptions = new ChatCompletionsOptions(deploymentName, new List<ChatRequestMessage>
            {
                new ChatRequestSystemMessage(GetSystemPrompt())
            });

            foreach (var tool in tools)
                chatOptions.Tools.Add(tool);
            chatOptions.ToolChoice = ChatCompletionsToolChoice.Auto;

            foreach (var msg in history)
            {
                if (msg.Role == "user")
                    chatOptions.Messages.Add(new ChatRequestUserMessage(msg.Content));
                else if (msg.Role == "assistant")
                    chatOptions.Messages.Add(new ChatRequestAssistantMessage(msg.Content));
            }
            chatOptions.Messages.Add(new ChatRequestUserMessage(userMessage));

            // Agentic loop – handle tool calls
            for (int iteration = 0; iteration < 5; iteration++)
            {
                var response = await openAIClient.GetChatCompletionsAsync(chatOptions);
                var choice = response.Value.Choices[0];

                if (choice.FinishReason == CompletionsFinishReason.ToolCalls && choice.Message.ToolCalls?.Count > 0)
                {
                    // Add assistant message (including tool calls)
                    chatOptions.Messages.Add(new ChatRequestAssistantMessage(choice.Message));

                    // Execute each tool call and add results
                    foreach (var toolCall in choice.Message.ToolCalls)
                    {
                        if (toolCall is ChatCompletionsFunctionToolCall functionCall)
                        {
                            _logger.LogInformation("Executing function: {Name}", functionCall.Name);
                            var result = await ExecuteFunctionAsync(functionCall.Name, functionCall.Arguments);
                            chatOptions.Messages.Add(new ChatRequestToolMessage(result, functionCall.Id));
                        }
                    }
                }
                else
                {
                    return choice.Message.Content ?? "I couldn't generate a response.";
                }
            }

            return "I reached the maximum iterations. Please try a more specific question.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Azure OpenAI");
            return $"**Error communicating with Azure OpenAI:** {ex.Message}\n\n" +
                   "**Troubleshooting:**\n" +
                   "- Ensure the managed identity has the 'Cognitive Services OpenAI User' role\n" +
                   "- Verify AZURE_CLIENT_ID is set to the managed identity client ID\n" +
                   "- Check that GenAISettings:Endpoint is correctly configured";
        }
    }

    private static List<ChatCompletionsFunctionToolDefinition> BuildFunctionToolDefinitions() => new()
    {
        new ChatCompletionsFunctionToolDefinition
        {
            Name = "get_expenses",
            Description = "Get a list of expenses with optional filters",
            Parameters = BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "userId": { "type": "integer", "description": "Filter by user ID (optional)" },
                    "statusId": { "type": "integer", "description": "Filter by status: 1=Draft, 2=Submitted, 3=Approved, 4=Rejected" },
                    "categoryId": { "type": "integer", "description": "Filter by category ID (optional)" }
                }
            }
            """)
        },
        new ChatCompletionsFunctionToolDefinition
        {
            Name = "get_expense_by_id",
            Description = "Get details of a specific expense by ID",
            Parameters = BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "id": { "type": "integer", "description": "The expense ID" }
                },
                "required": ["id"]
            }
            """)
        },
        new ChatCompletionsFunctionToolDefinition
        {
            Name = "create_expense",
            Description = "Create a new expense record",
            Parameters = BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "userId": { "type": "integer" },
                    "categoryId": { "type": "integer" },
                    "statusId": { "type": "integer", "description": "1=Draft, 2=Submitted" },
                    "amountMinor": { "type": "integer", "description": "Amount in pence (£1 = 100)" },
                    "expenseDate": { "type": "string", "description": "YYYY-MM-DD" },
                    "description": { "type": "string" }
                },
                "required": ["userId", "categoryId", "statusId", "amountMinor", "expenseDate"]
            }
            """)
        },
        new ChatCompletionsFunctionToolDefinition
        {
            Name = "approve_expense",
            Description = "Approve a submitted expense",
            Parameters = BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "expenseId": { "type": "integer" },
                    "reviewedBy": { "type": "integer", "description": "Manager's user ID" }
                },
                "required": ["expenseId", "reviewedBy"]
            }
            """)
        },
        new ChatCompletionsFunctionToolDefinition
        {
            Name = "reject_expense",
            Description = "Reject a submitted expense",
            Parameters = BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "expenseId": { "type": "integer" },
                    "reviewedBy": { "type": "integer", "description": "Manager's user ID" }
                },
                "required": ["expenseId", "reviewedBy"]
            }
            """)
        },
        new ChatCompletionsFunctionToolDefinition
        {
            Name = "get_users",
            Description = "Get all users",
            Parameters = BinaryData.FromString("""{ "type": "object", "properties": {} }""")
        },
        new ChatCompletionsFunctionToolDefinition
        {
            Name = "get_categories",
            Description = "Get all expense categories",
            Parameters = BinaryData.FromString("""{ "type": "object", "properties": {} }""")
        },
        new ChatCompletionsFunctionToolDefinition
        {
            Name = "get_statuses",
            Description = "Get all expense statuses",
            Parameters = BinaryData.FromString("""{ "type": "object", "properties": {} }""")
        }
    };

    private async Task<string> ExecuteFunctionAsync(string functionName, string arguments)
    {
        try
        {
            var args = JsonDocument.Parse(arguments).RootElement;
            return functionName switch
            {
                "get_expenses" => await CallApiAsync(BuildExpensesUrl(args)),
                "get_expense_by_id" => await CallApiAsync($"/api/expenses/{args.GetProperty("id").GetInt32()}"),
                "create_expense" => await PostApiAsync("/api/expenses", arguments),
                "approve_expense" => await PatchApiAsync(
                    $"/api/expenses/{args.GetProperty("expenseId").GetInt32()}/status",
                    JsonSerializer.Serialize(new { statusId = 3, reviewedBy = args.GetProperty("reviewedBy").GetInt32() })),
                "reject_expense" => await PatchApiAsync(
                    $"/api/expenses/{args.GetProperty("expenseId").GetInt32()}/status",
                    JsonSerializer.Serialize(new { statusId = 4, reviewedBy = args.GetProperty("reviewedBy").GetInt32() })),
                "get_users" => await CallApiAsync("/api/users"),
                "get_categories" => await CallApiAsync("/api/categories"),
                "get_statuses" => await CallApiAsync("/api/statuses"),
                _ => $"{{\"error\": \"Unknown function: {functionName}\"}}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {Name}", functionName);
            return $"{{\"error\": \"{ex.Message}\"}}";
        }
    }

    private static string BuildExpensesUrl(JsonElement args)
    {
        var query = new List<string>();
        if (args.TryGetProperty("userId", out var uid)) query.Add($"userId={uid.GetInt32()}");
        if (args.TryGetProperty("statusId", out var sid)) query.Add($"statusId={sid.GetInt32()}");
        if (args.TryGetProperty("categoryId", out var cid)) query.Add($"categoryId={cid.GetInt32()}");
        return "/api/expenses" + (query.Any() ? "?" + string.Join("&", query) : "");
    }

    private async Task<string> CallApiAsync(string path)
    {
        var response = await _httpClient.GetAsync(path);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> PostApiAsync(string path, string json)
    {
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(path, content);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> PatchApiAsync(string path, string json)
    {
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PatchAsync(path, content);
        return response.IsSuccessStatusCode ? "{\"success\": true}" : await response.Content.ReadAsStringAsync();
    }

    private static string GetSystemPrompt() => """
        You are an intelligent expense management assistant. You have access to real functions to interact with the expense database.
        
        Available functions: get_expenses, get_expense_by_id, create_expense, approve_expense, reject_expense, get_users, get_categories, get_statuses.
        
        Currency is GBP. Amounts are stored in pence (divide by 100 for pounds).
        Status IDs: 1=Draft, 2=Submitted, 3=Approved, 4=Rejected.
        
        Always use functions to fetch real data. Format amounts as £X.XX. Format lists clearly with bullet points.
        """;
}
