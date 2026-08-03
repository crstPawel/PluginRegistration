using PluginRegistration.Attributes;
using PluginRegistration.Core.Registration;

namespace PluginRegistration.Core.Sync;

public static class AttributeCodeGenerator
{
    public static string Generate(
        PluginRegistrationAttribute attribute,
        string indentation = "\r\n    ",
        string? pluginTypeName = null)
    {
        return GeneratePluginStep(attribute, indentation, pluginTypeName);
    }

    private static string GeneratePluginStep(
        PluginRegistrationAttribute attribute,
        string linePrefix,
        string? pluginTypeName)
    {
        var extras = BuildNamedParameters(attribute, pluginTypeName);

        var messagePart = TryFormatAsMessageTypeEnum(attribute.Message)
            ?? throw new PluginRegistrationException(
                $"Cannot generate code for unknown message '{attribute.Message}'. Add it to MessageTypeEnum.");

        // Single line: linePrefix is only the leading newline + class indentation.
        return
            $"{linePrefix}[PluginRegistration({messagePart}, \"{attribute.EntityLogicalName}\", StageEnum.{attribute.Stage}, ExecutionModeEnum.{attribute.ExecutionMode}, {FilteringAttributesParser.FormatForCode(attribute.FilteringAttributes)}, {attribute.ExecutionOrder}{extras})]";
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
            extras += $", Name = \"{attribute.Name}\"";
        }

        if (!attribute.Server)
        {
            extras += $", Server = {attribute.Server.ToString().ToLowerInvariant()}";
        }

        if (!string.IsNullOrWhiteSpace(attribute.Id))
        {
            extras += $", Id = \"{attribute.Id}\"";
        }

        if (attribute.ExecutionMode == ExecutionModeEnum.Asynchronous && attribute.DeleteAsyncOperation)
        {
            extras += ", DeleteAsyncOperation = true";
        }

        if (attribute.Action is not null)
        {
            extras += $", Action = PluginStepOperationEnum.{attribute.Action}";
        }

        return extras;
    }
}
