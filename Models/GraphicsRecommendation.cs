namespace GameLauncher.Models;

public enum GraphicsPreset { NãoRoda, Baixo, Médio, Alto, Ultra }

/// <summary>Resultado de recomendação gráfica para um jogo específico.</summary>
public class GraphicsRecommendation
{
    public string         GameName     { get; init; } = "";
    public GraphicsPreset Preset       { get; init; }
    public int            EstimatedFps { get; init; }
    public string         ShortMessage { get; init; } = "";
    public string         PresetColor  { get; init; } = "#FFFFFF";
    public string         PresetIcon   { get; init; } = "Speedometer";
    public List<string>   Details      { get; init; } = [];
    public PcHardwareProfile PcProfile { get; init; } = new();
    public bool IsGameKnown            { get; init; }

    public bool CanRun       => Preset != GraphicsPreset.NãoRoda;
    public string PresetName => Preset.ToString().Replace("ã", "a").Replace("é", "e");

    public string FpsDisplay => EstimatedFps > 0 ? $"~{EstimatedFps} fps" : "";
}
