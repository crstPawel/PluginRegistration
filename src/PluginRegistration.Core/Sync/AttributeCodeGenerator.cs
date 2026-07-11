using PluginRegistration.Attributes;
using PluginRegistration.Core.Registration;

namespace PluginRegistration.Core.Sync;

public static class AttributeCodeGenerator
{
    public static string Generate(
        CrmPluginRegistrationAttribute attribute,
        string indentation = "    ",
        string? pluginTypeName = null)
    {
        if (AttributeParser.IsPluginStepRegistration(attribute))
        {
            return GeneratePluginStep(attribute, indentation, pluginTypeName);
        }

        if (AttributeParser.IsCustomApiRegistration(attribute))
        {
            return $"{indentation}[CrmPluginRegistration(\"{attribute.Message}\")]";
        }

        return GenerateWorkflowActivity(attribute, indentation);
    }

    private static string GeneratePluginStep(
        CrmPluginRegistrationAttribute attribute,
        string indentation,
        string? pluginTypeName)
    {
        var extras = BuildNamedParameters(attribute, indentation, includeDescription: true, pluginTypeName);

        return string.Format(
            "{8}[CrmPluginRegistration(\"{0}\", {8}\"{1}\", StageEnum.{2}, ExecutionModeEnum.{3},{8}{4}, {5}{6}{8})]",
            attribute.Message,
            attribute.EntityLogicalName,
            attribute.Stage,
            attribute.ExecutionMode,
            FormatFilteringAttributesForCode(attribute.FilteringAttributes),
            attribute.ExecutionOrder,
            extras,
            indentation);
    }

    private static string FormatFilteringAttributesForCode(string? filteringAttributes)
    {
        if (string.IsNullOrWhiteSpace(filteringAttributes))
        {
            return "\"\"";
        }

        var parts = filteringAttributes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            return "\"\"";
        }

        if (parts.Length == 1)
        {
            return $"\"{parts[0]}\"";
        }

        return $"new[] {{ {string.Join(", ", parts.Select(part => $"\"{part}\""))} }}";
    }

    private static string GenerateWorkflowActivity(CrmPluginRegistrationAttribute attribute, string indentation)
    {
        var extras = BuildNamedParameters(attribute, indentation, includeDescription: false);
        return string.Format(
            "{6}[CrmPluginRegistration({6}\"{0}\", \"{1}\",\"{2}\",\"{3}\",IsolationModeEnum.{4}{5}{6})]",
            attribute.Name,
            attribute.FriendlyName,
            attribute.Description,
            attribute.GroupName,
            attribute.IsolationMode,
            extras,
            indentation);
    }

    private static string BuildNamedParameters(
        CrmPluginRegistrationAttribute attribute,
        string indentation,
        bool includeDescription,
        string? pluginTypeName = null)
    {
        var extras = string.Empty;

        if (!string.IsNullOrWhiteSpace(attribute.Name)
            && !string.IsNullOrWhiteSpace(pluginTypeName)
            && attribute.Stage is not null
            && !string.Equals(
                attribute.Name,
                PluginStepNameResolver.Resolve(pluginTypeName, attribute.Stage.Value),
                StringComparison.Ordinal))
        {
            extras += $"{indentation},Name = \"{attribute.Name}\"";
        }

        if (includeDescription && !string.IsNullOrWhiteSpace(attribute.Description))
        {
            extras += $"{indentation},Description = \"{attribute.Description}\"";
        }

        if (attribute.Offline)
        {
            extras += $"{indentation},Offline = {attribute.Offline.ToString().ToLowerInvariant()}";
        }

        if (!attribute.Server)
        {
            extras += $"{indentation},Server = {attribute.Server.ToString().ToLowerInvariant()}";
        }

        if (!string.IsNullOrWhiteSpace(attribute.Id))
        {
            extras += $"{indentation},Id = \"{attribute.Id}\"";
        }

        if (attribute.ExecutionMode == ExecutionModeEnum.Asynchronous && attribute.DeleteAsyncOperation)
        {
            extras += $"{indentation},DeleteAsyncOperation = true";
        }

        if (!string.IsNullOrWhiteSpace(attribute.UnSecureConfiguration))
        {
            extras += $"{indentation},UnSecureConfiguration = @\"{attribute.UnSecureConfiguration.Replace("\"", "\"\"")}\"";
        }

        if (!string.IsNullOrWhiteSpace(attribute.SecureConfiguration))
        {
            extras += $"{indentation},SecureConfiguration = @\"{attribute.SecureConfiguration.Replace("\"", "\"\"")}\"";
        }

        if (attribute.Action is not null)
        {
            extras += $"{indentation},Action = PluginStepOperationEnum.{attribute.Action}";
        }

        return extras;
    }
}