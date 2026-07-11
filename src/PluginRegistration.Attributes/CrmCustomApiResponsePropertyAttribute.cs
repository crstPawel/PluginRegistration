using System;

namespace PluginRegistration.Attributes;

/// <summary>
/// Declares a response property for a Custom API plugin type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class CrmCustomApiResponsePropertyAttribute : Attribute
{
    public CrmCustomApiResponsePropertyAttribute(
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
    public string? EntityLogicalName { get; set; }

    /// <summary>
    /// Links the response property to a specific Custom API when the class implements multiple APIs.
    /// </summary>
    public string? ApiUniqueName { get; set; }
}