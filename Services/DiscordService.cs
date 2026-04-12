using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameLauncher.Models;

namespace GameLauncher.Services;

public sealed class DiscordService : IDisposable
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameLauncher");
    private static readonly string TokenCachePath = Path.Combine(CacheDir, "discord_token.json");

    private const string AuthorizeUrl = "https://discord.com/api/oauth2/authorize";
    private const string TokenUrl = "https://discord.com/api/oauth2/token";
    private const string UserMeUrl = "https://discord.com/api/users/@me";
    private const string RedirectUri = "http://localhost:9547/callback";
    private const string Scope = "identify";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly string _clientId;
    private readonly string _clientSecret;

    private string? _accessToken;
    private string? _refreshToken;
    private DateTime _expiresAt;

    public bool IsLoggedIn => _accessToken is not null;
    public string? AccessToken => _accessToken;

    public static bool HasCachedToken() => File.Exists(TokenCachePath);

    public DiscordService(string clientId, string clientSecret)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    public async Task<bool> TrySilentLoginAsync()
    {
        try
        {
            if (!File.Exists(TokenCachePath))
                return false;

            var json = await File.ReadAllTextAsync(TokenCachePath);
            var cache = JsonSerializer.Deserialize<TokenCache>(json);
            if (cache is null || string.IsNullOrEmpty(cache.RefreshToken))
                return false;

            if (!string.IsNullOrEmpty(cache.AccessToken) && cache.ExpiresAt > DateTime.UtcNow.AddMinutes(5))
            {
                _accessToken = cache.AccessToken;
                _refreshToken = cache.RefreshToken;
                _expiresAt = cache.ExpiresAt;

                var profile = await GetProfileAsync();
                if (profile is not null)
                    return true;
            }

            var refreshed = await RefreshTokenAsync(cache.RefreshToken);
            return refreshed;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> LoginAsync()
    {
        try
        {
            var state = Guid.NewGuid().ToString("N");

            using var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:9547/");
            listener.Start();

            var authUrl = $"{AuthorizeUrl}?client_id={_clientId}&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                          $"&response_type=code&scope={Uri.EscapeDataString(Scope)}&state={state}&prompt=consent";
            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            var contextTask = listener.GetContextAsync();
            var completed = await Task.WhenAny(contextTask, Task.Delay(TimeSpan.FromMinutes(3)));
            if (completed != contextTask)
            {
                listener.Stop();
                return false;
            }

            var context = contextTask.Result;
            var query = context.Request.QueryString;
            var code = query["code"];
            var returnedState = query["state"];

            var responseHtml = "<html><body style='background:#0D0D0D;color:white;font-family:Segoe UI;display:flex;justify-content:center;align-items:center;height:100vh;margin:0'>" +
                               "<div style='text-align:center'><h2>✅ Discord conectado!</h2><p>Pode fechar esta aba e voltar ao GLauncher.</p></div></body></html>";

            var errorHtml = "<html><body style='background:#0D0D0D;color:white;font-family:Segoe UI;display:flex;justify-content:center;align-items:center;height:100vh;margin:0'>" +
                            "<div style='text-align:center'><h2>❌ Erro na autenticação</h2><p>Tente novamente no GLauncher.</p></div></body></html>";

            if (string.IsNullOrEmpty(code) || returnedState != state)
            {
                await WriteResponse(context.Response, errorHtml);
                listener.Stop();
                return false;
            }

            await WriteResponse(context.Response, responseHtml);
            listener.Stop();

            return await ExchangeCodeAsync(code);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ExchangeCodeAsync(string code)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri
            });

            var response = await _http.PostAsync(TokenUrl, content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var token = JsonSerializer.Deserialize<TokenResponse>(json);
            if (token is null || string.IsNullOrEmpty(token.AccessToken))
                return false;

            _accessToken = token.AccessToken;
            _refreshToken = token.RefreshToken;
            _expiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn);

            await SaveTokenCacheAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken
            });

            var response = await _http.PostAsync(TokenUrl, content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var token = JsonSerializer.Deserialize<TokenResponse>(json);
            if (token is null || string.IsNullOrEmpty(token.AccessToken))
                return false;

            _accessToken = token.AccessToken;
            _refreshToken = token.RefreshToken ?? refreshToken;
            _expiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn);

            await SaveTokenCacheAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<DiscordProfile?> GetProfileAsync()
    {
        if (string.IsNullOrEmpty(_accessToken))
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, UserMeUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<DiscordUserResponse>(json);
            if (user is null)
                return null;

            return new DiscordProfile
            {
                Id = user.Id ?? string.Empty,
                Username = user.Username ?? string.Empty,
                GlobalName = user.GlobalName ?? string.Empty,
                AvatarHash = user.Avatar ?? string.Empty,
                Discriminator = user.Discriminator ?? string.Empty
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        _accessToken = null;
        _refreshToken = null;
        _expiresAt = DateTime.MinValue;

        try { if (File.Exists(TokenCachePath)) File.Delete(TokenCachePath); } catch { }
        await Task.CompletedTask;
    }

    private async Task SaveTokenCacheAsync()
    {
        var cache = new TokenCache
        {
            AccessToken = _accessToken ?? string.Empty,
            RefreshToken = _refreshToken ?? string.Empty,
            ExpiresAt = _expiresAt
        };

        Directory.CreateDirectory(CacheDir);
        var json = JsonSerializer.Serialize(cache);
        await File.WriteAllTextAsync(TokenCachePath, json);
    }

    private static async Task WriteResponse(HttpListenerResponse response, string html)
    {
        var buffer = System.Text.Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

    public async Task<(List<DiscordDmChannel> Channels, string? Error)> GetDmChannelsAsync()
    {
        if (string.IsNullOrEmpty(_accessToken))
            return ([], "Token não disponível");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v10/users/@me/channels");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                if (status == 401 || status == 403)
                    return ([], "dm_not_available");
                return ([], $"Erro HTTP {status}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var channels = JsonSerializer.Deserialize<List<DmChannelResponse>>(json) ?? [];
            var result = new List<DiscordDmChannel>();

            foreach (var ch in channels)
            {
                if (ch.Type is not (1 or 3)) continue;

                var dm = new DiscordDmChannel
                {
                    ChannelId = ch.Id ?? "",
                    IsGroup = ch.Type == 3,
                    GroupName = ch.Name
                };

                if (ch.Recipients is not null)
                {
                    foreach (var r in ch.Recipients)
                    {
                        dm.Recipients.Add(new DiscordDmRecipient
                        {
                            Id = r.Id ?? "",
                            Username = r.Username ?? "",
                            GlobalName = r.GlobalName ?? "",
                            AvatarHash = r.Avatar
                        });
                    }
                }

                result.Add(dm);
            }

            return (result, null);
        }
        catch (Exception ex)
        {
            return ([], ex.Message);
        }
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    private sealed class DmChannelResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("recipients")]
        public List<DmRecipientResponse>? Recipients { get; set; }

        [JsonPropertyName("last_message_id")]
        public string? LastMessageId { get; set; }
    }

    private sealed class DmRecipientResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("global_name")]
        public string? GlobalName { get; set; }

        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }

    private sealed class DiscordUserResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("global_name")]
        public string? GlobalName { get; set; }

        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }

        [JsonPropertyName("discriminator")]
        public string? Discriminator { get; set; }
    }

    private sealed class TokenCache
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
