using CommunityToolkit.Mvvm.ComponentModel;

namespace GameLauncher.Models;

public enum ChatRole { User, Assistant, System }

public partial class AiChatMessage : ObservableObject
{
    public ChatRole Role { get; init; }

    [ObservableProperty]
    private string text = string.Empty;

    public bool IsUser      => Role == ChatRole.User;
    public bool IsAssistant => Role == ChatRole.Assistant;

    public string? ImageBase64 { get; init; }
}
