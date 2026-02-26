using ExpenseChat.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseChat.Pages;

public class ChatModel : PageModel
{
    private readonly IChatService _chatService;
    public ChatModel(IChatService chatService) => _chatService = chatService;

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message cannot be empty" });

        var response = await _chatService.ChatAsync(request.Message, request.History ?? new());
        return new JsonResult(new { response });
    }

    public record ChatRequest(string Message, List<ChatMessage>? History);
}
