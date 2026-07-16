using System.Reflection;
using PluginRegistration.Attributes;

namespace PluginRegistration.Core.Registration;

public static class AttributeParser
{
    public static PluginRegistrationAttribute Parse(CustomAttributeData data)
    {
        var arguments = data.ConstructorArguments.ToArray();

        if (arguments.Length != 6 || arguments[0].ArgumentType.Name != nameof(MessageTypeEnum))
        {
            throw new PluginRegistrationException(
                "Unsupported PluginRegistration attribute constructor. Use MessageTypeEnum as the first parameter for plugin steps.");
        }

        var attribute = PluginRegistrationAttribute.CreateStep(
            (MessageTypeEnum)Enum.ToObject(typeof(MessageTypeEnum), (int)arguments[0].Value!),
            (string)arguments[1].Value!,
            (StageEnum)Enum.ToObject(typeof(StageEnum), (int)arguments[2].Value!),
            (ExecutionModeEnum)Enum.ToObject(typeof(ExecutionModeEnum), (int)arguments[3].Value!),
            FilteringAttributesParser.ParseArray(arguments[4]),
            (int)arguments[5].Value!);

        foreach (var namedArgument in data.NamedArguments)
        {
            switch (namedArgument.MemberName)
            {
                case nameof(PluginRegistrationAttribute.Id):
                    attribute.Id = (string?)namedArgument.TypedValue.Value;
                    break;
                case nameof(PluginRegistrationAttribute.Name):
                    attribute.Name = (string?)namedArgument.TypedValue.Value;
                    break;
                case nameof(PluginRegistrationAttribute.DeleteAsyncOperation):
                    attribute.DeleteAsyncOperation = (bool)namedArgument.TypedValue.Value!;
                    break;
                case nameof(PluginRegistrationAttribute.UnSecureConfiguration):
                    attribute.UnSecureConfiguration = (string?)namedArgument.TypedValue.Value;
                    break;
                case nameof(PluginRegistrationAttribute.SecureConfiguration):
                    attribute.SecureConfiguration = (string?)namedArgument.TypedValue.Value;
                    break;
                case nameof(PluginRegistrationAttribute.Server):
                    attribute.Server = (bool)namedArgument.TypedValue.Value!;
                    break;
                case nameof(PluginRegistrationAttribute.Action):
                    attribute.Action = (PluginStepOperationEnum)namedArgument.TypedValue.Value!;
                    break;
                case nameof(PluginRegistrationAttribute.IsolationMode):
                    attribute.IsolationMode = (IsolationModeEnum)namedArgument.TypedValue.Value!;
                    break;
            }
        }

        return attribute;
    }

    public static bool IsPluginStepRegistration(PluginRegistrationAttribute attribute)
        => attribute.Stage is not null;
}