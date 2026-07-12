using System;

namespace PluginRegistration.Attributes;

/// <summary>
/// Declares a request parameter for a Custom API plugin type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class CustomApiRequestParameterAttribute : Attribute
{
    public CustomApiRequestParameterAttribute(
        string uniqueName,
        CustomApiParameterTypeEnum type)
    {
        UniqueName = uniqueName;
        Type = type;
    }

    public string UniqueName { get; }
    public CustomApiParameterTypeEnum Type { get; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public bool IsRequired { get; set; }
    public string? EntityLogicalName { get; set; }

    /// <summary>
    /// Links the parameter to a specific Custom API when the class implements multiple APIs.
    /// Defaults to the only Custom API on the class.
    /// </summary>
    public string? ApiUniqueName { get; set; }
}