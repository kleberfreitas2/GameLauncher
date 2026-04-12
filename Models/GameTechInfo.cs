using System.Text.Json.Serialization;

namespace GameLauncher.Models;

public class GameTechInfo
{
    public bool HasRayTracing { get; set; }
    public bool HasDLSS { get; set; }
    public string? DlssVersion { get; set; }
    public bool HasFSR { get; set; }
    public string? FsrVersion { get; set; }
    public bool HasXeSS { get; set; }
    public bool HasFrameGeneration { get; set; }
    public bool HasHDR { get; set; }
    public string? DirectXVersion { get; set; }
    public bool HasVulkan { get; set; }

    [JsonIgnore]
    public bool HasAnyTech =>
        HasRayTracing || HasDLSS || HasFSR || HasXeSS ||
        HasFrameGeneration || HasHDR || HasVulkan ||
        !string.IsNullOrEmpty(DirectXVersion);
}
