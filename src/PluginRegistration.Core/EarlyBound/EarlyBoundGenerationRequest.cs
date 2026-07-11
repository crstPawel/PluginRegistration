namespace PluginRegistration.Core.EarlyBound;

public sealed class EarlyBoundGenerationRequest
{
    public required string WorkingDirectory { get; init; }

    public string? ConfigFilePath { get; init; }

    public bool UseJsonConfig { get; init; }

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