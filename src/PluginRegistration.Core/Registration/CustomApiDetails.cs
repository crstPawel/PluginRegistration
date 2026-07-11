using Microsoft.Xrm.Sdk;

namespace PluginRegistration.Core.Registration;

public sealed class CustomApiDetails
{
    public required Entity Api { get; init; }
    public List<Entity> RequestParameters { get; init; } = [];
    public List<Entity> ResponseProperties { get; init; } = [];
}