using System.Reflection;
using PluginRegistration.Attributes;

namespace PluginRegistration.Core.Registration;

public static class CustomApiAttributeParser
{
    public static CustomApiRegistration Parse(CustomAttributeData data)
    {
        var arguments = data.ConstructorArguments.ToArray();
        if (arguments.Length < 1 || arguments[0].ArgumentType.Name != "String")
        {
            throw new PluginRegistrationException(
                "Unsupported CustomApiRegistration attribute constructor. Use UniqueName as the first parameter.");
        }

        // Current API: CustomApiRegistration(uniqueName) + named properties.
        // Legacy positional overloads (2-arg display name, 5-arg binding) are still accepted when reading older assemblies.
        var attribute = new CustomApiRegistration((string)arguments[0].Value!);

        if (arguments.Length >= 2 && arguments[1].ArgumentType.Name == "String")
        {
            attribute.DisplayName = (string?)arguments[1].Value;
        }

        if (arguments.Length >= 5)
        {
            attribute.ProcessingStepType = (CustomApiProcessingStepTypeEnum)Enum.ToObject(
                typeof(CustomApiProcessingStepTypeEnum),
                (int)arguments[2].Value!);
            attribute.CustomApiBindingType = (CustomApiBindingTypeEnum)Enum.ToObject(
                typeof(CustomApiBindingTypeEnum),
                (int)arguments[3].Value!);
            attribute.BoundEntityLogicalName = (string?)arguments[4].Value;
        }

        foreach (var namedArgument in data.NamedArguments)
        {
            switch (namedArgument.MemberName)
            {
                case nameof(CustomApiRegistration.DisplayName):
                    attribute.DisplayName = (string?)namedArgument.TypedValue.Value;
                    break;
                case "FriendlyName": // legacy alias used in older source / packages
                    attribute.DisplayName ??= (string?)namedArgument.TypedValue.Value;
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