using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GameLauncher.Services;

public sealed class DiscordRpcService : IDisposable
{
    private readonly string _clientId;
    private NamedPipeClientStream? _pipe;
    private bool _connected;
    private bool _authenticated;
    private string? _accessToken;
    private readonly object _lock = new();

    public bool IsConnected => _connected && _authenticated;

    public event Action<DiscordVoiceUser>? VoiceUserJoined;
    public event Action<DiscordVoiceUser>? VoiceUserLeft;
    public event Action<string, string>? NotificationReceived;
    public event Action<string>? VoiceChannelJoined;
    public event Action? VoiceChannelLeft;
    public event Action<bool>? MuteChanged;
    public event Action<bool>? DeafChanged;

    public bool IsMuted { get; private set; }
    public bool IsDeafened { get; private set; }
    public string? CurrentVoiceChannelId { get; private set; }
    public string? CurrentVoiceChannelName { get; private set; }

    public DiscordRpcService(string clientId)
    {
        _clientId = clientId;
    }

    public bool TryConnect()
    {
        if (_connected && _pipe is { IsConnected: true })
            return true;

        for (int i = 0; i < 10; i++)
        {
            try
            {
                var pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}",
                    PipeDirection.InOut, PipeOptions.Asynchronous);
                pipe.Connect(500);

                if (!pipe.IsConnected)
                {
                    pipe.Dispose();
                    continue;
                }

                _pipe = pipe;

                var handshake = JsonSerializer.Serialize(new { v = 1, client_id = _clientId });
                WriteFrame(0, handshake);
                var response = ReadFrame();

                if (response is null)
                {
                    pipe.Dispose();
                    _pipe = null;
                    continue;
                }

                _connected = true;
                return true;
            }
            catch
            {
                continue;
            }
        }

        return false;
    }

    public bool Authorize()
    {
        if (!_connected || _pipe is null) return false;

        try
        {
            var payload = new
            {
                cmd = "AUTHORIZE",
                args = new
                {
                    client_id = _clientId,
                    scopes = new[] { "rpc", "rpc.voice.read", "rpc.voice.write", "rpc.notifications.read" }
                },
                nonce = Guid.NewGuid().ToString()
            };

            WriteFrame(1, JsonSerializer.Serialize(payload));
            var response = ReadFrame();
            if (response is null) return false;

            var node = JsonNode.Parse(response);
            var code = node?["data"]?["code"]?.GetValue<string>();
            return !string.IsNullOrEmpty(code);
        }
        catch
        {
            return false;
        }
    }

    public bool Authenticate(string accessToken)
    {
        if (!_connected || _pipe is null) return false;

        try
        {
            _accessToken = accessToken;

            var payload = new
            {
                cmd = "AUTHENTICATE",
                args = new { access_token = accessToken },
                nonce = Guid.NewGuid().ToString()
            };

            WriteFrame(1, JsonSerializer.Serialize(payload));
            var response = ReadFrame();
            if (response is null) return false;

            var node = JsonNode.Parse(response);
            var evt = node?["evt"]?.GetValue<string>();
            if (evt == "ERROR") return false;

            _authenticated = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public List<DiscordGuild> GetGuilds()
    {
        var result = new List<DiscordGuild>();
        if (!IsConnected) return result;

        try
        {
            var response = SendCommand("GET_GUILDS");
            var guilds = response?["data"]?["guilds"]?.AsArray();
            if (guilds is null) return result;

            foreach (var g in guilds)
            {
                if (g is null) continue;
                result.Add(new DiscordGuild
                {
                    Id = g["id"]?.GetValue<string>() ?? "",
                    Name = g["name"]?.GetValue<string>() ?? ""
                });
            }
        }
        catch { }

        return result;
    }

    public List<DiscordVoiceChannel> GetVoiceChannels(string guildId)
    {
        var result = new List<DiscordVoiceChannel>();
        if (!IsConnected) return result;

        try
        {
            var response = SendCommand("GET_CHANNELS", new { guild_id = guildId });
            var channels = response?["data"]?["channels"]?.AsArray();
            if (channels is null) return result;

            foreach (var c in channels)
            {
                if (c is null) continue;
                var type = c["type"]?.GetValue<int>() ?? 0;
                if (type != 2) continue; // 2 = voice channel

                result.Add(new DiscordVoiceChannel
                {
                    Id = c["id"]?.GetValue<string>() ?? "",
                    Name = c["name"]?.GetValue<string>() ?? "",
                    GuildId = guildId
                });
            }
        }
        catch { }

        return result;
    }

    public List<DiscordVoiceUser> GetChannelVoiceUsers(string channelId)
    {
        var result = new List<DiscordVoiceUser>();
        if (!IsConnected) return result;

        try
        {
            var response = SendCommand("GET_CHANNEL", new { channel_id = channelId });
            var states = response?["data"]?["voice_states"]?.AsArray();
            if (states is null) return result;

            foreach (var s in states)
            {
                if (s is null) continue;
                var user = s["user"];
                result.Add(new DiscordVoiceUser
                {
                    UserId = user?["id"]?.GetValue<string>() ?? "",
                    Username = user?["username"]?.GetValue<string>() ?? "",
                    GlobalName = user?["global_name"]?.GetValue<string>(),
                    IsMuted = s["voice_state"]?["mute"]?.GetValue<bool>() ?? false,
                    IsSelfMuted = s["voice_state"]?["self_mute"]?.GetValue<bool>() ?? false,
                    IsSelfDeafened = s["voice_state"]?["self_deaf"]?.GetValue<bool>() ?? false
                });
            }
        }
        catch { }

        return result;
    }

    public bool JoinVoiceChannel(string channelId)
    {
        if (!IsConnected) return false;

        try
        {
            var response = SendCommand("SELECT_VOICE_CHANNEL", new { channel_id = channelId });
            var data = response?["data"];
            if (data is null || data.GetValueKind() == JsonValueKind.Null) return false;

            CurrentVoiceChannelId = channelId;
            CurrentVoiceChannelName = data["name"]?.GetValue<string>();

            SubscribeVoiceEvents(channelId);

            VoiceChannelJoined?.Invoke(CurrentVoiceChannelName ?? channelId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void LeaveVoiceChannel()
    {
        if (!IsConnected) return;

        try
        {
            if (CurrentVoiceChannelId is not null)
                UnsubscribeVoiceEvents(CurrentVoiceChannelId);

            SendCommand("SELECT_VOICE_CHANNEL", new { channel_id = (string?)null });

            CurrentVoiceChannelId = null;
            CurrentVoiceChannelName = null;
            VoiceChannelLeft?.Invoke();
        }
        catch { }
    }

    public void SetMute(bool mute)
    {
        if (!IsConnected) return;

        try
        {
            SendCommand("SET_VOICE_SETTINGS", new { mute });
            IsMuted = mute;
            MuteChanged?.Invoke(mute);
        }
        catch { }
    }

    public void SetDeafen(bool deaf)
    {
        if (!IsConnected) return;

        try
        {
            SendCommand("SET_VOICE_SETTINGS", new { deaf });
            IsDeafened = deaf;
            DeafChanged?.Invoke(deaf);
        }
        catch { }
    }

    public void ToggleMute() => SetMute(!IsMuted);
    public void ToggleDeafen() => SetDeafen(!IsDeafened);

    public (bool mute, bool deaf) GetVoiceSettings()
    {
        if (!IsConnected) return (false, false);

        try
        {
            var response = SendCommand("GET_VOICE_SETTINGS");
            var data = response?["data"];
            bool mute = data?["mute"]?.GetValue<bool>() ?? false;
            bool deaf = data?["deaf"]?.GetValue<bool>() ?? false;
            IsMuted = mute;
            IsDeafened = deaf;
            return (mute, deaf);
        }
        catch
        {
            return (false, false);
        }
    }

    public void SubscribeNotifications()
    {
        if (!IsConnected) return;

        try
        {
            var payload = new
            {
                cmd = "SUBSCRIBE",
                evt = "NOTIFICATION_CREATE",
                nonce = Guid.NewGuid().ToString()
            };

            WriteFrame(1, JsonSerializer.Serialize(payload));
            ReadFrame();
        }
        catch { }
    }

    public void ProcessPendingEvents()
    {
        if (!IsConnected || _pipe is null) return;

        try
        {
            while (_pipe.IsConnected)
            {
                if (!HasData()) break;

                var frame = ReadFrame();
                if (frame is null) break;

                var node = JsonNode.Parse(frame);
                var evt = node?["evt"]?.GetValue<string>();

                switch (evt)
                {
                    case "VOICE_STATE_CREATE":
                        HandleVoiceStateCreate(node?["data"]);
                        break;
                    case "VOICE_STATE_DELETE":
                        HandleVoiceStateDelete(node?["data"]);
                        break;
                    case "NOTIFICATION_CREATE":
                        HandleNotification(node?["data"]);
                        break;
                }
            }
        }
        catch { }
    }

    private void HandleVoiceStateCreate(JsonNode? data)
    {
        if (data is null) return;
        var user = data["user"];
        VoiceUserJoined?.Invoke(new DiscordVoiceUser
        {
            UserId = user?["id"]?.GetValue<string>() ?? "",
            Username = user?["username"]?.GetValue<string>() ?? "",
            GlobalName = user?["global_name"]?.GetValue<string>(),
            IsMuted = data["voice_state"]?["mute"]?.GetValue<bool>() ?? false,
            IsSelfMuted = data["voice_state"]?["self_mute"]?.GetValue<bool>() ?? false,
            IsSelfDeafened = data["voice_state"]?["self_deaf"]?.GetValue<bool>() ?? false
        });
    }

    private void HandleVoiceStateDelete(JsonNode? data)
    {
        if (data is null) return;
        var user = data["user"];
        VoiceUserLeft?.Invoke(new DiscordVoiceUser
        {
            UserId = user?["id"]?.GetValue<string>() ?? "",
            Username = user?["username"]?.GetValue<string>() ?? ""
        });
    }

    private void HandleNotification(JsonNode? data)
    {
        if (data is null) return;
        var title = data["title"]?.GetValue<string>() ?? "";
        var body = data["body"]?.GetValue<string>() ?? "";
        NotificationReceived?.Invoke(title, body);
    }

    private void SubscribeVoiceEvents(string channelId)
    {
        Subscribe("VOICE_STATE_CREATE", new { channel_id = channelId });
        Subscribe("VOICE_STATE_DELETE", new { channel_id = channelId });
    }

    private void UnsubscribeVoiceEvents(string channelId)
    {
        Unsubscribe("VOICE_STATE_CREATE", new { channel_id = channelId });
        Unsubscribe("VOICE_STATE_DELETE", new { channel_id = channelId });
    }

    private void Subscribe(string evt, object args)
    {
        try
        {
            var payload = new { cmd = "SUBSCRIBE", evt, args, nonce = Guid.NewGuid().ToString() };
            WriteFrame(1, JsonSerializer.Serialize(payload));
            ReadFrame();
        }
        catch { }
    }

    private void Unsubscribe(string evt, object args)
    {
        try
        {
            var payload = new { cmd = "UNSUBSCRIBE", evt, args, nonce = Guid.NewGuid().ToString() };
            WriteFrame(1, JsonSerializer.Serialize(payload));
            ReadFrame();
        }
        catch { }
    }

    private JsonNode? SendCommand(string cmd, object? args = null)
    {
        lock (_lock)
        {
            var payload = args is not null
                ? new { cmd, args, nonce = Guid.NewGuid().ToString() }
                : (object)new { cmd, nonce = Guid.NewGuid().ToString() };

            WriteFrame(1, JsonSerializer.Serialize(payload));
            var response = ReadFrame();
            return response is not null ? JsonNode.Parse(response) : null;
        }
    }

    private bool HasData()
    {
        try
        {
            if (_pipe is null || !_pipe.IsConnected) return false;
            // NamedPipeClientStream doesn't have a clean way to check
            // Use a zero-timeout read approach or just return false here
            // For simplicity in the polling model, we skip async events
            return false;
        }
        catch { return false; }
    }

    private void WriteFrame(int opcode, string data)
    {
        if (_pipe is null) return;

        var dataBytes = Encoding.UTF8.GetBytes(data);
        var frame = new byte[8 + dataBytes.Length];

        BitConverter.GetBytes(opcode).CopyTo(frame, 0);
        BitConverter.GetBytes(dataBytes.Length).CopyTo(frame, 4);
        dataBytes.CopyTo(frame, 8);

        _pipe.Write(frame, 0, frame.Length);
        _pipe.Flush();
    }

    private string? ReadFrame()
    {
        if (_pipe is null) return null;

        var header = new byte[8];
        int bytesRead = 0;
        while (bytesRead < 8)
        {
            int read = _pipe.Read(header, bytesRead, 8 - bytesRead);
            if (read == 0) return null;
            bytesRead += read;
        }

        int length = BitConverter.ToInt32(header, 4);
        if (length <= 0 || length > 65536) return null;

        var body = new byte[length];
        bytesRead = 0;
        while (bytesRead < length)
        {
            int read = _pipe.Read(body, bytesRead, length - bytesRead);
            if (read == 0) return null;
            bytesRead += read;
        }

        return Encoding.UTF8.GetString(body);
    }

    public void Disconnect()
    {
        try
        {
            if (_connected && _pipe is { IsConnected: true })
                WriteFrame(2, "{}");
        }
        catch { }

        _connected = false;
        _authenticated = false;
        _pipe?.Dispose();
        _pipe = null;
    }

    public void Dispose() => Disconnect();
}

public class DiscordGuild
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public class DiscordVoiceChannel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string GuildId { get; set; } = "";
}

public class DiscordVoiceUser
{
    public string UserId { get; set; } = "";
    public string Username { get; set; } = "";
    public string? GlobalName { get; set; }
    public bool IsMuted { get; set; }
    public bool IsSelfMuted { get; set; }
    public bool IsSelfDeafened { get; set; }

    public string DisplayName => !string.IsNullOrEmpty(GlobalName) ? GlobalName : Username;

    public string StatusIcon =>
        IsSelfDeafened ? "🔇" :
        IsSelfMuted || IsMuted ? "🔈" :
        "🔊";
}
