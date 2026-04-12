using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameLauncher.Models;
using Microsoft.Identity.Client;

namespace GameLauncher.Services;

public sealed class XboxLiveService : IDisposable
{
    private static readonly string[] XboxScopes = ["Xboxlive.signin", "Xboxlive.offline_access"];
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameLauncher");
    private static readonly string TokenCachePath = Path.Combine(CacheDir, "xbox_token_cache.dat");

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly string _clientId;
    private IPublicClientApplication? _msalApp;

    private string? _xstsToken;
    private string? _userHash;
    private string? _xuid;

    public bool IsLoggedIn => _xstsToken is not null;
    public string? Xuid => _xuid;

    public XboxLiveService(string clientId)
    {
        _clientId = clientId;
    }

    private IPublicClientApplication GetMsalApp()
    {
        if (_msalApp is not null) return _msalApp;

        _msalApp = PublicClientApplicationBuilder
            .Create(_clientId)
            .WithAuthority("https://login.microsoftonline.com/consumers")
            .WithRedirectUri("http://localhost")
            .Build();

        _msalApp.UserTokenCache.SetBeforeAccess(args =>
        {
            if (File.Exists(TokenCachePath))
                args.TokenCache.DeserializeMsalV3(File.ReadAllBytes(TokenCachePath));
        });
        _msalApp.UserTokenCache.SetAfterAccess(args =>
        {
            if (args.HasStateChanged)
            {
                Directory.CreateDirectory(CacheDir);
                File.WriteAllBytes(TokenCachePath, args.TokenCache.SerializeMsalV3());
            }
        });

        return _msalApp;
    }

    public async Task<bool> TrySilentLoginAsync()
    {
        try
        {
            var app = GetMsalApp();
            var accounts = await app.GetAccountsAsync();
            var account = accounts.FirstOrDefault();
            if (account is null) return false;

            var authResult = await app.AcquireTokenSilent(XboxScopes, account).ExecuteAsync();
            if (string.IsNullOrEmpty(authResult.AccessToken)) return false;

            var xblToken = await GetXblTokenAsync(authResult.AccessToken);
            if (xblToken is null) return false;

            var xsts = await GetXstsTokenAsync(xblToken.Token);
            if (xsts is null) return false;

            _xstsToken = xsts.Token;
            _userHash = xsts.UserHash;
            _xuid = xsts.Xuid;
            return true;
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
            var app = GetMsalApp();

            AuthenticationResult? authResult = null;

            var accounts = await app.GetAccountsAsync();
            var account = accounts.FirstOrDefault();
            if (account is not null)
            {
                try
                {
                    authResult = await app.AcquireTokenSilent(XboxScopes, account).ExecuteAsync();
                }
                catch (MsalUiRequiredException) { }
            }

            authResult ??= await app.AcquireTokenInteractive(XboxScopes)
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync();

            if (string.IsNullOrEmpty(authResult.AccessToken))
                return false;

            var xblToken = await GetXblTokenAsync(authResult.AccessToken);
            if (xblToken is null)
                return false;

            var xsts = await GetXstsTokenAsync(xblToken.Token);
            if (xsts is null)
                return false;

            _xstsToken = xsts.Token;
            _userHash = xsts.UserHash;
            _xuid = xsts.Xuid;

            return true;
        }
        catch (MsalServiceException ex) when (ex.ErrorCode == "invalid_scope")
        {
            System.Windows.MessageBox.Show(
                "O escopo Xboxlive.signin não foi aceito.\n\n" +
                "Verifique no portal Azure se:\n" +
                "• A plataforma 'Aplicativos móveis e de área de trabalho' está adicionada\n" +
                "• O redirect URI está configurado\n" +
                "• 'Permitir fluxos de cliente público' está ativado\n\n" +
                $"Detalhes: {ex.Message}",
                "Xbox Live — Erro de Escopo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return false;
        }
        catch (MsalClientException ex)
        {
            System.Windows.MessageBox.Show(
                $"Erro de autenticação MSAL:\n{ex.Message}\n\nCódigo: {ex.ErrorCode}",
                "Xbox Live — Erro", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        _xstsToken = null;
        _userHash = null;
        _xuid = null;

        if (_msalApp is not null)
        {
            var accounts = await _msalApp.GetAccountsAsync();
            foreach (var account in accounts)
            {
                await _msalApp.RemoveAsync(account);
            }
        }

        try { if (File.Exists(TokenCachePath)) File.Delete(TokenCachePath); } catch { }
    }

    public async Task<int> GetLibraryGamesCountAsync()
    {
        if (!IsLoggedIn || _xuid is null)
            return 0;

        var count = await TryGetGameCollectionCountAsync();
        if (count >= 0)
            return count;

        return await GetTitleHubPcGamesCountAsync();
    }

    private async Task<int> TryGetGameCollectionCountAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                "https://emerald.xboxservices.com/xboxcomfd/v3/gameCollection" +
                "?CollectionType=MyGames&SkipItems=0&MaxItems=1&Filters=PcGame");

            request.Headers.Add("Authorization", $"XBL3.0 x={_userHash};{_xstsToken}");
            request.Headers.Add("x-xbl-contract-version", "4");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var collection = JsonSerializer.Deserialize<GameCollectionResponse>(json);

            return collection?.TotalItemsCount
                ?? collection?.TotalItems
                ?? collection?.Items?.Count
                ?? -1;
        }
        catch
        {
            return -1;
        }
    }

    private async Task<int> GetTitleHubPcGamesCountAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://titlehub.xboxlive.com/users/xuid({_xuid})/titles/titlehistory/decoration/Detail");

            request.Headers.Add("Authorization", $"XBL3.0 x={_userHash};{_xstsToken}");
            request.Headers.Add("x-xbl-contract-version", "2");
            request.Headers.Add("Accept-Language", "en-US");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var titleHistory = JsonSerializer.Deserialize<TitleHistoryResponse>(responseJson);

            if (titleHistory?.Titles is null)
                return 0;

            return titleHistory.Titles.Count(t =>
                string.Equals(t.Type, "Game", StringComparison.OrdinalIgnoreCase) &&
                t.Devices?.Any(d => d.Equals("PC", StringComparison.OrdinalIgnoreCase) ||
                                    d.Equals("Win32", StringComparison.OrdinalIgnoreCase)) == true);
        }
        catch
        {
            return 0;
        }
    }

    public async Task<XboxProfile?> GetProfileAsync()
    {
        if (!IsLoggedIn || _xuid is null)
            return null;

        try
        {
            var requestBody = new
            {
                userIds = new[] { _xuid },
                settings = new[]
                {
                    "GameDisplayName", "GameDisplayPicRaw",
                    "Gamerscore", "Gamertag", "AccountTier"
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://profile.xboxlive.com/users/batch/profile/settings")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            request.Headers.Add("Authorization", $"XBL3.0 x={_userHash};{_xstsToken}");
            request.Headers.Add("x-xbl-contract-version", "2");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var profileResponse = JsonSerializer.Deserialize<ProfileBatchResponse>(responseJson);

            if (profileResponse?.ProfileUsers is not { Count: > 0 })
                return null;

            var user = profileResponse.ProfileUsers[0];
            var profile = new XboxProfile { Xuid = _xuid };

            foreach (var setting in user.Settings)
            {
                switch (setting.Id)
                {
                    case "Gamertag":
                        profile.Gamertag = setting.Value;
                        break;
                    case "GameDisplayName":
                        profile.GameDisplayName = setting.Value;
                        break;
                    case "GameDisplayPicRaw":
                        profile.AvatarUrl = setting.Value;
                        break;
                    case "Gamerscore":
                        if (int.TryParse(setting.Value, out var gs))
                            profile.Gamerscore = gs;
                        break;
                    case "AccountTier":
                        profile.AccountTier = setting.Value;
                        break;
                }
            }

            return profile;
        }
        catch
        {
            return null;
        }
    }

    public List<Game> ScanXboxInstalledGames()
    {
        var games = new List<Game>();
        var foundPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var xboxFolders = new List<string>();

        foreach (var drive in System.IO.DriveInfo.GetDrives())
        {
            if (drive.DriveType != System.IO.DriveType.Fixed) continue;
            var xboxPath = System.IO.Path.Combine(drive.Name, "XboxGames");
            if (System.IO.Directory.Exists(xboxPath))
                xboxFolders.Add(xboxPath);
        }

        foreach (var folder in xboxFolders)
        {
            try
            {
                foreach (var gameDir in System.IO.Directory.GetDirectories(folder))
                {
                    var contentDir = System.IO.Path.Combine(gameDir, "Content");
                    var searchDir = System.IO.Directory.Exists(contentDir) ? contentDir : gameDir;

                    var exeFiles = System.IO.Directory.GetFiles(searchDir, "*.exe",
                        System.IO.SearchOption.AllDirectories);

                    var mainExe = exeFiles
                        .Select(f => new System.IO.FileInfo(f))
                        .Where(f => f.Length > 100_000)
                        .Where(f => !IsExcludedExe(f.Name))
                        .OrderByDescending(f => f.Length)
                        .FirstOrDefault();

                    if (mainExe is null || foundPaths.Contains(mainExe.FullName))
                        continue;

                    foundPaths.Add(mainExe.FullName);

                    var gameName = System.IO.Path.GetFileName(gameDir);

                    games.Add(new Game
                    {
                        Name = gameName,
                        ExecutablePath = mainExe.FullName,
                        InstallDirectory = gameDir,
                        IconPath = IconExtractor.ExtractIcon(mainExe.FullName)
                    });
                }
            }
            catch { }
        }

        return games;
    }

    private static readonly HashSet<string> ExcludedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "uninstall", "unins", "crash", "report", "updater", "patcher",
        "config", "settings", "setup", "installer", "redist",
        "easyanticheat", "battleye", "vc_redist", "dxsetup"
    };

    private static bool IsExcludedExe(string fileName)
    {
        return ExcludedKeywords.Any(k =>
            fileName.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<XblTokenResponse?> GetXblTokenAsync(string msaAccessToken)
    {
        try
        {
            var requestBody = new
            {
                RelyingParty = "http://auth.xboxlive.com",
                TokenType = "JWT",
                Properties = new
                {
                    AuthMethod = "RPS",
                    SiteName = "user.auth.xboxlive.com",
                    RpsTicket = $"d={msaAccessToken}"
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://user.auth.xboxlive.com/user/authenticate")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var xblResponse = JsonSerializer.Deserialize<XblAuthResponse>(responseJson);

            if (xblResponse is null) return null;

            var uhs = xblResponse.DisplayClaims?.Xui?.FirstOrDefault()?.Uhs;
            return new XblTokenResponse { Token = xblResponse.Token, UserHash = uhs ?? string.Empty };
        }
        catch
        {
            return null;
        }
    }

    private async Task<XstsTokenResponse?> GetXstsTokenAsync(string xblToken)
    {
        try
        {
            var requestBody = new
            {
                RelyingParty = "http://xboxlive.com",
                TokenType = "JWT",
                Properties = new
                {
                    UserTokens = new[] { xblToken },
                    SandboxId = "RETAIL"
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://xsts.auth.xboxlive.com/xsts/authorize")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var xstsResponse = JsonSerializer.Deserialize<XblAuthResponse>(responseJson);

            if (xstsResponse is null) return null;

            var xui = xstsResponse.DisplayClaims?.Xui?.FirstOrDefault();
            return new XstsTokenResponse
            {
                Token = xstsResponse.Token,
                UserHash = xui?.Uhs ?? string.Empty,
                Xuid = xui?.Xid ?? string.Empty
            };
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _http.Dispose();
    }


    private class XblTokenResponse
    {
        public string Token { get; set; } = string.Empty;
        public string UserHash { get; set; } = string.Empty;
    }

    private class XstsTokenResponse
    {
        public string Token { get; set; } = string.Empty;
        public string UserHash { get; set; } = string.Empty;
        public string Xuid { get; set; } = string.Empty;
    }

    private class XblAuthResponse
    {
        [JsonPropertyName("Token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("DisplayClaims")]
        public DisplayClaimsDto? DisplayClaims { get; set; }
    }

    private class DisplayClaimsDto
    {
        [JsonPropertyName("xui")]
        public List<XuiEntry>? Xui { get; set; }
    }

    private class XuiEntry
    {
        [JsonPropertyName("uhs")]
        public string? Uhs { get; set; }

        [JsonPropertyName("xid")]
        public string? Xid { get; set; }
    }

    private class TitleHistoryResponse
    {
        [JsonPropertyName("titles")]
        public List<TitleEntry>? Titles { get; set; }
    }

    private class TitleEntry
    {
        [JsonPropertyName("titleId")]
        public string TitleId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("devices")]
        public List<string>? Devices { get; set; }
    }

    private class GameCollectionResponse
    {
        [JsonPropertyName("totalItemsCount")]
        public int? TotalItemsCount { get; set; }

        [JsonPropertyName("totalItems")]
        public int? TotalItems { get; set; }

        [JsonPropertyName("items")]
        public List<GameCollectionItem>? Items { get; set; }
    }

    private class GameCollectionItem
    {
        [JsonPropertyName("titleId")]
        public string TitleId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private class ProfileBatchResponse
    {
        [JsonPropertyName("profileUsers")]
        public List<ProfileUser> ProfileUsers { get; set; } = [];
    }

    private class ProfileUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("settings")]
        public List<ProfileSetting> Settings { get; set; } = [];
    }

    private class ProfileSetting
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }
}
