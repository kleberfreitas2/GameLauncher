using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace GameLauncher.Services;

public static class DiscordWebhookService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task<bool> SendMessageAsync(string webhookUrl, string message, string? username = null)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl) || string.IsNullOrWhiteSpace(message))
            return false;

        try
        {
            var payload = new Dictionary<string, string> { ["content"] = message };
            if (!string.IsNullOrEmpty(username))
                payload["username"] = username;

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await Http.PostAsync(webhookUrl, content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
