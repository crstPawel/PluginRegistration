namespace PluginRegistration.Core.EarlyBound;

public sealed class EarlyBoundGenerationRequest
{
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Path to DLaB Early Bound Generator V2 XML config (earlyboundgenerator.xml).
    /// Relative paths are resolved against <see cref="WorkingDirectory"/>.
    /// </summary>
    public string? ConfigFilePath { get; init; }

    public string? OutputDirectory { get; set; }

    public string? Namespace { get; set; }

    public string? ServiceContextName { get; set; }

    public string? EntitiesWhitelist { get; set; }

    public bool? GenerateMessages { get; set; }

    public bool? GenerateGlobalOptionSets { get; set; }

    public bool? OverwriteExistingFiles { get; set; }

    public bool InitConfigOnly { get; init; }

    public bool ForceInitConfig { get; init; }
}
