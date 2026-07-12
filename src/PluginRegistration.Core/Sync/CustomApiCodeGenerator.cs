using PluginRegistration.Attributes;
using PluginRegistration.Core.Registration;

namespace PluginRegistration.Core.Sync;

public static class CustomApiCodeGenerator
{
    public static IEnumerable<string> GenerateBlocks(
        CustomApiRegistration attribute,
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

    private static string GenerateMainAttribute(CustomApiRegistration attribute, string indentation)
    {
        var uniqueName = attribute.UniqueName;
        var displayName = attribute.DisplayName ?? uniqueName;
        var bindingType = attribute.CustomApiBindingType;
        var processingStepType = attribute.ProcessingStepType;
        var boundEntity = attribute.BoundEntityLogicalName ?? string.Empty;

        string constructor;
        if (bindingType != CustomApiBindingTypeEnum.Global
            || processingStepType != CustomApiProcessingStepTypeEnum.None
            || !string.IsNullOrWhiteSpace(boundEntity))
        {
            constructor =
                $"\"{Escape(uniqueName)}\", \"{Escape(displayName)}\", CustomApiProcessingStepTypeEnum.{processingStepType}, CustomApiBindingTypeEnum.{bindingType}, \"{Escape(boundEntity)}\"";
        }
        else if (!string.Equals(displayName, uniqueName, StringComparison.Ordinal))
        {
            constructor = $"\"{Escape(uniqueName)}\", \"{Escape(displayName)}\"";
        }
        else
        {
            constructor = $"\"{Escape(uniqueName)}\"";
        }

        var extras = string.Empty;

        if (constructor.StartsWith($"\"{Escape(uniqueName)}\"", StringComparison.Ordinal)
            && !string.Equals(displayName, uniqueName, StringComparison.Ordinal)
            && !constructor.Contains("\", \"", StringComparison.Ordinal))
        {
            extras += $"{indentation},FriendlyName = \"{Escape(displayName)}\"";
        }

        if (!string.IsNullOrWhiteSpace(attribute.Description))
        {
            extras += $"{indentation},Description = \"{Escape(attribute.Description)}\"";
        }

        if (attribute.IsFunction)
        {
            extras += $"{indentation},IsFunction = true";
        }

        if (attribute.IsPrivate)
        {
            extras += $"{indentation},IsPrivate = true";
        }

        if (!string.IsNullOrWhiteSpace(attribute.ExecutePrivilegeName))
        {
            extras += $"{indentation},ExecutePrivilegeName = \"{Escape(attribute.ExecutePrivilegeName)}\"";
        }

        return $"{indentation}[CustomApiRegistration({constructor}{extras}{indentation})]";
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

        return $"{indentation}[CustomApiRequestParameter(\"{Escape(parameter.UniqueName)}\", CustomApiParameterTypeEnum.{parameter.Type}{extras}{indentation})]";
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

        return $"{indentation}[CustomApiResponseProperty(\"{Escape(property.UniqueName)}\", CustomApiParameterTypeEnum.{property.Type}{extras}{indentation})]";
    }

    private static string Escape(string value) => value.Replace("\"", "\"\"");
}