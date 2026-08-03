using PluginRegistration.Attributes;

namespace PluginRegistration.Core.Registration;

public sealed record PluginStepImageModel
{
    public string Name { get; init; } = string.Empty;
    public ImageTypeEnum ImageType { get; init; }
    public string[] Attributes { get; init; } = [];

    /// <summary>
    /// Optional SDK message used to disambiguate when a plugin type has multiple steps.
    /// </summary>
    public string? Message { get; init; }
}
