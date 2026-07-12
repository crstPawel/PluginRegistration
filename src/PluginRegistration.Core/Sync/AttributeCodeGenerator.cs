using PluginRegistration.Attributes;
using PluginRegistration.Core.Registration;

namespace PluginRegistration.Core.Sync;

public static class AttributeCodeGenerator
{
    public static string Generate(
        PluginRegistrationAttribute attribute,
        string indentation = "    ",
        string? pluginTypeName = null)
    {
        if (AttributeParser.IsPluginStepRegistration(attribute))
        {
            return GeneratePluginStep(attribute, indentation, pluginTypeName);
        }

        if (AttributeParser.IsCustomApiRegistration(attribute))
        {
            return $"{indentation}[PluginRegistration(\"{attribute.Message}\")]";
        }

        return GenerateWorkflowActivity(attribute, indentation);
    }

    private static string GeneratePluginStep(
        PluginRegistrationAttribute attribute,
        string indentation,
        string? pluginTypeName)
    {
        var extras = BuildNamedParameters(attribute, indentation, includeDescription: true, pluginTypeName);

        // Prefer MessageTypeEnum when the message is one of the common types
        string messagePart = TryFormatAsMessageTypeEnum(attribute.Message)
            ?? $"\"{attribute.Message}\"";

        return string.Format(
            "{8}[PluginRegistration({8}{0}, {8}\"{1}\", StageEnum.{2}, ExecutionModeEnum.{3},{8}{4}, {5}{6}{8})]",
            messagePart,
            attribute.EntityLogicalName,
            attribute.Stage,
            attribute.ExecutionMode,
            FormatFilteringAttributesForCode(attribute.FilteringAttributes),
            attribute.ExecutionOrder,
            extras,
            indentation);
    }

    private static string? TryFormatAsMessageTypeEnum(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        // Common messages that have a corresponding value in MessageTypeEnum
        return message switch
        {
            "Create" => "MessageTypeEnum.Create",
            "Update" => "MessageTypeEnum.Update",
            "Delete" => "MessageTypeEnum.Delete",
            "Retrieve" => "MessageTypeEnum.Retrieve",
            "RetrieveMultiple" => "MessageTypeEnum.RetrieveMultiple",
            _ => null
        };
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

    private static string GenerateWorkflowActivity(PluginRegistrationAttribute attribute, string indentation)
    {
        var extras = BuildNamedParameters(attribute, indentation, includeDescription: false);
        return string.Format(
            "{6}[PluginRegistration({6}\"{0}\", \"{1}\",\"{2}\",\"{3}\",IsolationModeEnum.{4}{5}{6})]",
            attribute.Name,
            attribute.FriendlyName,
            attribute.Description,
            attribute.GroupName,
            attribute.IsolationMode,
            extras,
            indentation);
    }

    private static string BuildNamedParameters(
        PluginRegistrationAttribute attribute,
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