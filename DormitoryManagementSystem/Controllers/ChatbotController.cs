using System.Security.Claims;
using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DormitoryManagementSystem.Controllers;

[Authorize(Roles = RoleNames.Student)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ChatbotController(IChatbotService chatbot) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting("student-chat")]
    [RequestSizeLimit(40_000)]
    public async Task<IActionResult> Ask([FromBody] ChatbotQuestion? question, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || question is null || string.IsNullOrWhiteSpace(question.Message))
            return BadRequest(new { success = false, text = "Vui lòng nhập câu hỏi hợp lệ, tối đa 1.000 ký tự." });

        var studentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(studentUserId)) return Forbid();

        var answer = await chatbot.AskAsync(studentUserId, question, cancellationToken);
        return StatusCode(answer.StatusCode, answer);
    }
}
