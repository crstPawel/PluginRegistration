using PluginRegistration.Attributes;

namespace PluginRegistration.Core.Registration;

public sealed record PluginStepImageModel
{
    public string Name { get; init; } = string.Empty;
    public ImageTypeEnum ImageType { get; init; }
    public string? Attributes { get; init; }
}