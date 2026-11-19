using System.Net;
using System.Text;
using System.Text.Json;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DormitoryManagementSystem.Tests;

public sealed class GeminiChatbotServiceTests
{
    [Fact]
    public async Task Request_keeps_key_in_header_and_separates_instructions_from_conversation()
    {
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent", request.RequestUri!.ToString());
            Assert.Equal("test-secret", Assert.Single(request.Headers.GetValues("x-goog-api-key")));
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.DoesNotContain("test-secret", body);
            using var json = JsonDocument.Parse(body);
            Assert.Contains("không có quyền", json.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
            var contents = json.RootElement.GetProperty("contents");
            Assert.Equal(3, contents.GetArrayLength());
            Assert.Equal("user", contents[0].GetProperty("role").GetString());
            Assert.Equal("model", contents[1].GetProperty("role").GetString());
            Assert.Equal("Chuyển phòng thế nào?", contents[2].GetProperty("parts")[0].GetProperty("text").GetString());
            return JsonResponse("""{"candidates":[{"finishReason":"STOP","content":{"parts":[{"text":"Nội bộ","thought":true},{"text":"Chọn phòng."},{"text":"Gửi đơn."}]}}]}""");
        });
        var answer = await CreateService(handler).AskAsync(new ChatbotQuestion
        {
            Message = " Chuyển phòng thế nào? ",
            History = [new() { Role = "user", Text = "Xin chào" }, new() { Role = "model", Text = "Chào bạn" }]
        });

        Assert.True(answer.Success);
        Assert.Equal("Chọn phòng.\nGửi đơn.", answer.Text);
    }

    [Fact]
    public async Task Missing_key_does_not_send_a_request()
    {
        var handler = new StubHandler((_, _) => throw new InvalidOperationException("Should not send."));
        var answer = await CreateService(handler, string.Empty).AskAsync(Question());
        Assert.False(answer.Success);
        Assert.Equal(503, answer.StatusCode);
    }

    [Fact]
    public async Task Invalid_model_does_not_allow_a_different_endpoint()
    {
        var handler = new StubHandler((_, _) => throw new InvalidOperationException("Should not send."));
        var answer = await CreateService(handler, model: "../other:action").AskAsync(Question());
        Assert.False(answer.Success);
        Assert.Equal(503, answer.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, 429)]
    [InlineData(HttpStatusCode.Unauthorized, 503)]
    [InlineData(HttpStatusCode.Forbidden, 503)]
    [InlineData(HttpStatusCode.InternalServerError, 503)]
    public async Task Provider_errors_are_safe_and_keep_actionable_status(HttpStatusCode providerStatus, int expectedStatus)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(providerStatus)
        {
            Content = new StringContent("test-secret: private provider diagnostics")
        }));
        var answer = await CreateService(handler).AskAsync(Question());
        Assert.False(answer.Success);
        Assert.Equal(expectedStatus, answer.StatusCode);
        Assert.DoesNotContain("test-secret", answer.Text);
        Assert.DoesNotContain("private provider", answer.Text);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"candidates\":[]}")]
    [InlineData("{\"candidates\":null}")]
    [InlineData("{\"candidates\":[{\"finishReason\":\"SAFETY\",\"content\":{\"parts\":[{\"text\":\"blocked text\"}]}}]}")]
    [InlineData("{\"candidates\":[{\"content\":{\"parts\":[{\"thought\":true,\"text\":\"private thought\"}]}}]}")]
    public async Task Blocked_or_missing_text_is_not_presented_as_an_answer(string response)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse(response)));
        var answer = await CreateService(handler).AskAsync(Question());
        Assert.False(answer.Success);
        Assert.Equal(422, answer.StatusCode);
        Assert.DoesNotContain("blocked text", answer.Text);
        Assert.DoesNotContain("private thought", answer.Text);
    }

    [Fact]
    public async Task Invalid_json_returns_a_safe_error()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse("not json")));
        var answer = await CreateService(handler).AskAsync(Question());
        Assert.Equal(502, answer.StatusCode);
        Assert.False(answer.Success);
    }

    [Fact]
    public async Task Network_failure_returns_a_safe_error()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("private diagnostics"));
        var answer = await CreateService(handler).AskAsync(Question());
        Assert.Equal(503, answer.StatusCode);
        Assert.DoesNotContain("private diagnostics", answer.Text);
    }

    [Fact]
    public async Task Provider_timeout_returns_a_retry_message()
    {
        var handler = new StubHandler((_, _) => throw new TaskCanceledException());
        var answer = await CreateService(handler).AskAsync(Question());
        Assert.Equal(504, answer.StatusCode);
    }

    [Fact]
    public async Task Browser_cancellation_is_propagated()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var handler = new StubHandler((_, token) => Task.FromCanceled<HttpResponseMessage>(token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService(handler).AskAsync(Question(), source.Token));
    }

    private static ChatbotQuestion Question() => new() { Message = "Đăng ký phòng thế nào?" };

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static GeminiChatbotService CreateService(HttpMessageHandler handler, string key = "test-secret", string model = "gemini-3.5-flash-lite") =>
        new(new HttpClient(handler), Options.Create(new GeminiChatbotOptions { ApiKey = key, Model = model }),
            NullLogger<GeminiChatbotService>.Instance);

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
