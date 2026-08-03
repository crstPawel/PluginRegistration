using PluginRegistration.Attributes;
using PluginRegistration.Core.Registration;

namespace PluginRegistration.Core.Sync;

public static class CustomApiCodeGenerator
{
    public static IEnumerable<string> GenerateBlocks(
        CustomApiRegistration attribute,
        IEnumerable<CustomApiParameterModel> requestParameters,
        IEnumerable<CustomApiParameterModel> responseProperties,
        string linePrefix)
    {
        yield return GenerateMainAttribute(attribute, linePrefix);

        foreach (var parameter in requestParameters)
        {
            yield return GenerateRequestParameter(parameter, linePrefix);
        }

        foreach (var property in responseProperties)
        {
            yield return GenerateResponseProperty(property, linePrefix);
        }
    }

    private static string GenerateMainAttribute(CustomApiRegistration attribute, string linePrefix)
    {
        var uniqueName = attribute.UniqueName;
        var extras = string.Empty;

        if (!string.IsNullOrWhiteSpace(attribute.DisplayName)
            && !string.Equals(attribute.DisplayName, uniqueName, StringComparison.Ordinal))
        {
            extras += $", DisplayName = \"{Escape(attribute.DisplayName)}\"";
        }

        if (!string.IsNullOrWhiteSpace(attribute.Description))
        {
            extras += $", Description = \"{Escape(attribute.Description)}\"";
        }

        if (attribute.CustomApiBindingType != CustomApiBindingTypeEnum.Global)
        {
            extras += $", CustomApiBindingType = CustomApiBindingTypeEnum.{attribute.CustomApiBindingType}";
        }

        if (attribute.ProcessingStepType != CustomApiProcessingStepTypeEnum.None)
        {
            extras += $", ProcessingStepType = CustomApiProcessingStepTypeEnum.{attribute.ProcessingStepType}";
        }

        if (!string.IsNullOrWhiteSpace(attribute.BoundEntityLogicalName))
        {
            extras += $", BoundEntityLogicalName = \"{Escape(attribute.BoundEntityLogicalName)}\"";
        }

        if (attribute.IsFunction)
        {
            extras += ", IsFunction = true";
        }

        if (attribute.IsPrivate)
        {
            extras += ", IsPrivate = true";
        }

        if (!string.IsNullOrWhiteSpace(attribute.ExecutePrivilegeName))
        {
            extras += $", ExecutePrivilegeName = \"{Escape(attribute.ExecutePrivilegeName)}\"";
        }

        return $"{linePrefix}[CustomApiRegistration(\"{Escape(uniqueName)}\"{extras})]";
    }

    private static string GenerateRequestParameter(CustomApiParameterModel parameter, string linePrefix)
    {
        var extras = string.Empty;

        if (!string.Equals(parameter.DisplayName, parameter.UniqueName, StringComparison.Ordinal))
        {
            extras += $", DisplayName = \"{Escape(parameter.DisplayName)}\"";
        }

        if (!string.IsNullOrWhiteSpace(parameter.Description))
        {
            extras += $", Description = \"{Escape(parameter.Description)}\"";
        }

        if (parameter.IsRequired)
        {
            extras += ", IsRequired = true";
        }

        if (!string.IsNullOrWhiteSpace(parameter.EntityLogicalName))
        {
            extras += $", EntityLogicalName = \"{Escape(parameter.EntityLogicalName)}\"";
        }

        if (!string.IsNullOrWhiteSpace(parameter.ApiUniqueName))
        {
            extras += $", ApiUniqueName = \"{Escape(parameter.ApiUniqueName)}\"";
        }

        return $"{linePrefix}[CustomApiRequestParameter(\"{Escape(parameter.UniqueName)}\", CustomApiParameterTypeEnum.{parameter.Type}{extras})]";
    }

    private static string GenerateResponseProperty(CustomApiParameterModel property, string linePrefix)
    {
        var extras = string.Empty;

        if (!string.Equals(property.DisplayName, property.UniqueName, StringComparison.Ordinal))
        {
            extras += $", DisplayName = \"{Escape(property.DisplayName)}\"";
        }

        if (!string.IsNullOrWhiteSpace(property.Description))
        {
            extras += $", Description = \"{Escape(property.Description)}\"";
        }

        if (!string.IsNullOrWhiteSpace(property.EntityLogicalName))
        {
            extras += $", EntityLogicalName = \"{Escape(property.EntityLogicalName)}\"";
        }

        if (!string.IsNullOrWhiteSpace(property.ApiUniqueName))
        {
            extras += $", ApiUniqueName = \"{Escape(property.ApiUniqueName)}\"";
        }

        return $"{linePrefix}[CustomApiResponseProperty(\"{Escape(property.UniqueName)}\", CustomApiParameterTypeEnum.{property.Type}{extras})]";
    }

    private static string Escape(string value) => value.Replace("\"", "\"\"");
}
