using PluginRegistration.Attributes;
using PluginRegistration.Core.Registration;

namespace PluginRegistration.Core.Sync;

public static class CustomApiCodeGenerator
{
    public static IEnumerable<string> GenerateBlocks(
        CrmPluginRegistrationAttribute attribute,
        IEnumerable<CustomApiParameterModel> requestParameters,
        IEnumerable<CustomApiParameterModel> responseProperties,
        string indentation)
    {
        yield return GenerateMainAttribute(attribute, indentation);

        foreach (var parameter in requestParameters)
        {
            yield return GenerateRequestParameter(parameter, indentation);
        }

        foreach (var property in responseProperties)
        {
            yield return GenerateResponseProperty(property, indentation);
        }
    }

    private static string GenerateMainAttribute(CrmPluginRegistrationAttribute attribute, string indentation)
    {
        var extras = string.Empty;

        if (!string.IsNullOrWhiteSpace(attribute.FriendlyName))
        {
            extras += $"{indentation},FriendlyName = \"{Escape(attribute.FriendlyName)}\"";
        }

        if (!string.IsNullOrWhiteSpace(attribute.Description))
        {
            extras += $"{indentation},Description = \"{Escape(attribute.Description)}\"";
        }

        if (attribute.CustomApiBindingType != CustomApiBindingTypeEnum.Global)
        {
            extras += $"{indentation},CustomApiBindingType = CustomApiBindingTypeEnum.{attribute.CustomApiBindingType}";
        }

        if (!string.IsNullOrWhiteSpace(attribute.BoundEntityLogicalName))
        {
            extras += $"{indentation},BoundEntityLogicalName = \"{Escape(attribute.BoundEntityLogicalName)}\"";
        }

        if (attribute.IsFunction)
        {
            extras += $"{indentation},IsFunction = true";
        }

        if (attribute.IsPrivate)
        {
            extras += $"{indentation},IsPrivate = true";
        }

        if (attribute.AllowedCustomProcessingStepType != CustomApiProcessingStepTypeEnum.None)
        {
            extras += $"{indentation},AllowedCustomProcessingStepType = CustomApiProcessingStepTypeEnum.{attribute.AllowedCustomProcessingStepType}";
        }

        return $"{indentation}[CrmPluginRegistration(\"{Escape(attribute.Message!)}\"{extras}{indentation})]";
    }

    private static string GenerateRequestParameter(CustomApiParameterModel parameter, string indentation)
    {
        var extras = string.Empty;

        if (!string.Equals(parameter.DisplayName, parameter.UniqueName, StringComparison.Ordinal))
        {
            extras += $"{indentation},DisplayName = \"{Escape(parameter.DisplayName)}\"";
        }

        if (!string.IsNullOrWhiteSpace(parameter.Description))
        {
            extras += $"{indentation},Description = \"{Escape(parameter.Description)}\"";
        }

        if (parameter.IsRequired)
        {
            extras += $"{indentation},IsRequired = true";
        }

        if (!string.IsNullOrWhiteSpace(parameter.EntityLogicalName))
        {
            extras += $"{indentation},EntityLogicalName = \"{Escape(parameter.EntityLogicalName)}\"";
        }

        if (!string.IsNullOrWhiteSpace(parameter.ApiUniqueName))
        {
            extras += $"{indentation},ApiUniqueName = \"{Escape(parameter.ApiUniqueName)}\"";
        }

        return $"{indentation}[CrmCustomApiRequestParameter(\"{Escape(parameter.UniqueName)}\", CustomApiParameterTypeEnum.{parameter.Type}{extras}{indentation})]";
    }

    private static string GenerateResponseProperty(CustomApiParameterModel property, string indentation)
    {
        var extras = string.Empty;

        if (!string.Equals(property.DisplayName, property.UniqueName, StringComparison.Ordinal))
        {
            extras += $"{indentation},DisplayName = \"{Escape(property.DisplayName)}\"";
        }

        if (!string.IsNullOrWhiteSpace(property.Description))
        {
            extras += $"{indentation},Description = \"{Escape(property.Description)}\"";
        }

        if (!string.IsNullOrWhiteSpace(property.EntityLogicalName))
        {
            extras += $"{indentation},EntityLogicalName = \"{Escape(property.EntityLogicalName)}\"";
        }

        if (!string.IsNullOrWhiteSpace(property.ApiUniqueName))
        {
            extras += $"{indentation},ApiUniqueName = \"{Escape(property.ApiUniqueName)}\"";
        }

        return $"{indentation}[CrmCustomApiResponseProperty(\"{Escape(property.UniqueName)}\", CustomApiParameterTypeEnum.{property.Type}{extras}{indentation})]";
    }

    private static string Escape(string value) => value.Replace("\"", "\"\"");
}