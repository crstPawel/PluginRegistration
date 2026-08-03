namespace PluginRegistration.Core.Registration;

/// <summary>
/// Classic solution component type codes (picklist values).
/// </summary>
/// <remarks>
/// Do <b>not</b> use 371/372 for Custom API — those are <b>Connector</b> types in Dataverse.
/// Custom API tables are solution-aware data components; use the entity
/// <c>ObjectTypeCode</c> from metadata (see <see cref="SolutionComponentTypeResolver"/>).
/// </remarks>
internal static class SolutionComponentTypes
{
    public const int PluginAssembly = 91;
    public const int SdkMessageProcessingStep = 92;
    public const int SdkMessageProcessingStepImage = 93;
}
