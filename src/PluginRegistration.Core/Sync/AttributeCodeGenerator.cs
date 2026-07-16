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
        return GeneratePluginStep(attribute, indentation, pluginTypeName);
    }

    private static string GeneratePluginStep(
        PluginRegistrationAttribute attribute,
        string indentation,
        string? pluginTypeName)
    {
        var extras = BuildNamedParameters(attribute, indentation, pluginTypeName);

        var messagePart = TryFormatAsMessageTypeEnum(attribute.Message)
            ?? throw new PluginRegistrationException(
                $"Cannot generate code for unknown message '{attribute.Message}'. Add it to MessageTypeEnum.");

        return string.Format(
            "{8}[PluginRegistration({8}{0}, {8}\"{1}\", StageEnum.{2}, ExecutionModeEnum.{3},{8}{4}, {5}{6}{8})]",
            messagePart,
            attribute.EntityLogicalName,
            attribute.Stage,
            attribute.ExecutionMode,
            FilteringAttributesParser.FormatForCode(attribute.FilteringAttributes),
            attribute.ExecutionOrder,
            extras,
            indentation);
    }

    private static string? TryFormatAsMessageTypeEnum(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        return Enum.TryParse<MessageTypeEnum>(message, true, out var messageType)
            ? $"MessageTypeEnum.{messageType}"
            : null;
    }

    private static string BuildNamedParameters(
        PluginRegistrationAttribute attribute,
        string indentation,
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