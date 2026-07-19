using System.Reflection;
using PluginRegistration.Attributes;

namespace PluginRegistration.Core.Registration;

public static class CustomApiAttributeParser
{
    public static CustomApiRegistration Parse(CustomAttributeData data)
    {
        var arguments = data.ConstructorArguments.ToArray();
        CustomApiRegistration attribute;

        switch (arguments.Length)
        {
            case 1 when arguments[0].ArgumentType.Name == "String":
                attribute = new CustomApiRegistration((string)arguments[0].Value!);
                break;
            case 2 when arguments[0].ArgumentType.Name == "String":
                attribute = new CustomApiRegistration(
                    (string)arguments[0].Value!,
                    (string)arguments[1].Value!);
                break;
            case 5 when arguments[0].ArgumentType.Name == "String":
                attribute = new CustomApiRegistration(
                    (string)arguments[0].Value!,
                    (string)arguments[1].Value!,
                    (CustomApiProcessingStepTypeEnum)Enum.ToObject(
                        typeof(CustomApiProcessingStepTypeEnum),
                        (int)arguments[2].Value!),
                    (CustomApiBindingTypeEnum)Enum.ToObject(
                        typeof(CustomApiBindingTypeEnum),
                        (int)arguments[3].Value!),
                    (string)arguments[4].Value!);
                break;
            default:
                throw new PluginRegistrationException(
                    "Unsupported CustomApiRegistration attribute constructor.");
        }

        foreach (var namedArgument in data.NamedArguments)
        {
            switch (namedArgument.MemberName)
            {
                case nameof(CustomApiRegistration.DisplayName):
                    attribute.DisplayName = (string?)namedArgument.TypedValue.Value;
                    break;
                case nameof(CustomApiRegistration.FriendlyName):
                    attribute.FriendlyName = (string?)namedArgument.TypedValue.Value;
                    break;
                case nameof(CustomApiRegistration.Description):
                    attribute.Description = (string?)namedArgument.TypedValue.Value;
                    break;
                case nameof(CustomApiRegistration.ExecutePrivilegeName):
                    attribute.ExecutePrivilegeName = (string?)namedArgument.TypedValue.Value;
                    break;
                case nameof(CustomApiRegistration.BoundEntityLogicalName):
                    attribute.BoundEntityLogicalName = (string?)namedArgument.TypedValue.Value;
                    break;
                case nameof(CustomApiRegistration.ProcessingStepType):
                    attribute.ProcessingStepType =
                        (CustomApiProcessingStepTypeEnum)namedArgument.TypedValue.Value!;
                    break;
                case nameof(CustomApiRegistration.CustomApiBindingType):
                    attribute.CustomApiBindingType =
                        (CustomApiBindingTypeEnum)namedArgument.TypedValue.Value!;
                    break;
                case nameof(CustomApiRegistration.IsFunction):
                    attribute.IsFunction = (bool)namedArgument.TypedValue.Value!;
                    break;
                case nameof(CustomApiRegistration.IsPrivate):
                    attribute.IsPrivate = (bool)namedArgument.TypedValue.Value!;
                    break;
            }
        }

        return attribute;
    }
}