namespace GameLauncher.ViewModels;

public enum CompatStatus { Unknown, Compatible, Incompatible }

public class TechCompatItem
{
    public string TechName { get; init; } = "";
    public string Detail { get; init; } = "";
    public CompatStatus Status { get; init; }
    public string StatusText { get; init; } = "";
    public string StatusColor { get; init; } = "#888888";
    public string IconKind { get; init; } = "Gpu";
}
