using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace GameLauncher.Services;

public sealed class DiscordRichPresenceService : IDisposable
{
    private readonly string _clientId;
    private NamedPipeClientStream? _pipe;
    private bool _connected;

    public DiscordRichPresenceService(string clientId)
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
                ReadFrame();

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

    public void SetActivity(string gameName, DateTimeOffset startTime)
    {
        if (!_connected || _pipe is null || !_pipe.IsConnected)
        {
            if (!TryConnect())
                return;
        }

        try
        {
            var payload = new
            {
                cmd = "SET_ACTIVITY",
                args = new
                {
                    pid = Environment.ProcessId,
                    activity = new
                    {
                        details = gameName,
                        state = "via GLauncher",
                        timestamps = new
                        {
                            start = startTime.ToUnixTimeSeconds()
                        },
                        assets = new
                        {
                            large_text = "GLauncher"
                        }
                    }
                },
                nonce = Guid.NewGuid().ToString()
            };

            var json = JsonSerializer.Serialize(payload);
            WriteFrame(1, json);

            try { ReadFrame(); } catch { }
        }
        catch
        {
            _connected = false;
        }
    }

    public void ClearActivity()
    {
        if (!_connected || _pipe is null || !_pipe.IsConnected)
            return;

        try
        {
            var payload = new
            {
                cmd = "SET_ACTIVITY",
                args = new
                {
                    pid = Environment.ProcessId,
                    activity = (object?)null
                },
                nonce = Guid.NewGuid().ToString()
            };

            var json = JsonSerializer.Serialize(payload);
            WriteFrame(1, json);

            try { ReadFrame(); } catch { }
        }
        catch
        {
            _connected = false;
        }
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
        var bytesRead = 0;
        while (bytesRead < 8)
        {
            var read = _pipe.Read(header, bytesRead, 8 - bytesRead);
            if (read == 0) return null;
            bytesRead += read;
        }

        var length = BitConverter.ToInt32(header, 4);
        if (length <= 0 || length > 65536) return null;

        var body = new byte[length];
        bytesRead = 0;
        while (bytesRead < length)
        {
            var read = _pipe.Read(body, bytesRead, length - bytesRead);
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
            {
                ClearActivity();
                WriteFrame(2, "{}");
            }
        }
        catch { }

        _connected = false;
        _pipe?.Dispose();
        _pipe = null;
    }

    public void Dispose()
    {
        Disconnect();
    }
}
