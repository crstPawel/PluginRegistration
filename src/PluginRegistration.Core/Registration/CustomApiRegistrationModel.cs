using PluginRegistration.Attributes;

namespace PluginRegistration.Core.Registration;

public sealed class CustomApiRegistrationModel
{
    public string UniqueName { get; init; } = string.Empty;
    public string PluginTypeName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public CustomApiBindingTypeEnum BindingType { get; init; } = CustomApiBindingTypeEnum.Global;
    public bool IsFunction { get; init; }
    public bool IsPrivate { get; init; }
    public string? BoundEntityLogicalName { get; init; }
    public CustomApiProcessingStepTypeEnum AllowedCustomProcessingStepType { get; init; } =
        CustomApiProcessingStepTypeEnum.None;
    public List<CustomApiParameterModel> RequestParameters { get; init; } = [];
    public List<CustomApiParameterModel> ResponseProperties { get; init; } = [];
}

public sealed record CustomApiParameterModel
{
    public string UniqueName { get; init; } = string.Empty;
    public CustomApiParameterTypeEnum Type { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsRequired { get; init; }
    public string? EntityLogicalName { get; init; }
    public string? ApiUniqueName { get; init; }
}