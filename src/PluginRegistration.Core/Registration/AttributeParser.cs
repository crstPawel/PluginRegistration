using System.Reflection;
using PluginRegistration.Attributes;

namespace PluginRegistration.Core.Registration;

public static class AttributeParser
{
    public static CrmPluginRegistrationAttribute Parse(CustomAttributeData data)
    {
        CrmPluginRegistrationAttribute? attribute = null;
        var arguments = data.ConstructorArguments.ToArray();

        if (arguments.Length == 6 && arguments[0].ArgumentType.Name == "String")
        {
            attribute = CreatePluginStepAttribute(
                (string)arguments[0].Value!,
                (string)arguments[1].Value!,
                (StageEnum)Enum.ToObject(typeof(StageEnum), (int)arguments[2].Value!),
                (ExecutionModeEnum)Enum.ToObject(typeof(ExecutionModeEnum), (int)arguments[3].Value!),
                arguments[4],
                (int)arguments[5].Value!);
        }
        else if (arguments.Length == 6 && arguments[0].ArgumentType.Name == "MessageNameEnum")
        {
            attribute = CreatePluginStepAttribute(
                ((MessageNameEnum)Enum.ToObject(typeof(MessageNameEnum), (int)arguments[0].Value!)).ToString(),
                (string)arguments[1].Value!,
                (StageEnum)Enum.ToObject(typeof(StageEnum), (int)arguments[2].Value!),
                (ExecutionModeEnum)Enum.ToObject(typeof(ExecutionModeEnum), (int)arguments[3].Value!),
                arguments[4],
                (int)arguments[5].Value!);
        }
        else if (arguments.Length == 5 && arguments[0].ArgumentType.Name == "String")
        {
            attribute = new CrmPluginRegistrationAttribute(
                (string)arguments[0].Value!,
                (string)arguments[1].Value!,
                (string)arguments[2].Value!,
                (string)arguments[3].Value!,
                (IsolationModeEnum)Enum.ToObject(typeof(IsolationModeEnum), (int)arguments[4].Value!));
        }
        else if (arguments.Length == 1 && arguments[0].ArgumentType.Name == "String")
        {
            attribute = new CrmPluginRegistrationAttribute((string)arguments[0].Value!);
        }
        else
        {
            throw new PluginRegistrationException("Unsupported CrmPluginRegistration attribute constructor.");
        }

        foreach (var namedArgument in data.NamedArguments)
        {
            switch (namedArgument.MemberName)
            {
                case "Id":
                    attribute.Id = (string?)namedArgument.TypedValue.Value;
                    break;
                case "Name":
                    attribute.Name = (string?)namedArgument.TypedValue.Value;
                    break;
                case "FriendlyName":
                    attribute.FriendlyName = (string?)namedArgument.TypedValue.Value;
                    break;
                case "GroupName":
                    attribute.GroupName = (string?)namedArgument.TypedValue.Value;
                    break;
                case "Description":
                    attribute.Description = (string?)namedArgument.TypedValue.Value;
                    break;
                case "DeleteAsyncOperation":
                    attribute.DeleteAsyncOperation = (bool)namedArgument.TypedValue.Value!;
                    break;
                case "UnSecureConfiguration":
                    attribute.UnSecureConfiguration = (string?)namedArgument.TypedValue.Value;
                    break;
                case "SecureConfiguration":
                    attribute.SecureConfiguration = (string?)namedArgument.TypedValue.Value;
                    break;
                case "Offline":
                    attribute.Offline = (bool)namedArgument.TypedValue.Value!;
                    break;
                case "Server":
                    attribute.Server = (bool)namedArgument.TypedValue.Value!;
                    break;
                case "Action":
                    attribute.Action = (PluginStepOperationEnum)namedArgument.TypedValue.Value!;
                    break;
                case "CustomApiBindingType":
                    attribute.CustomApiBindingType = (CustomApiBindingTypeEnum)namedArgument.TypedValue.Value!;
                    break;
                case "IsFunction":
                    attribute.IsFunction = (bool)namedArgument.TypedValue.Value!;
                    break;
                case "IsPrivate":
                    attribute.IsPrivate = (bool)namedArgument.TypedValue.Value!;
                    break;
                case "BoundEntityLogicalName":
                    attribute.BoundEntityLogicalName = (string?)namedArgument.TypedValue.Value;
                    break;
                case "AllowedCustomProcessingStepType":
                    attribute.AllowedCustomProcessingStepType =
                        (CustomApiProcessingStepTypeEnum)namedArgument.TypedValue.Value!;
                    break;
            }
        }

        return attribute;
    }

    private static CrmPluginRegistrationAttribute CreatePluginStepAttribute(
        string message,
        string entityLogicalName,
        StageEnum stage,
        ExecutionModeEnum executionMode,
        CustomAttributeTypedArgument filteringAttributesArgument,
        int executionOrder)
    {
        var filteringAttributes = FilteringAttributesParser.Parse(filteringAttributesArgument);

        return new CrmPluginRegistrationAttribute(
            message,
            entityLogicalName,
            stage,
            executionMode,
            filteringAttributes,
            executionOrder);
    }

    public static bool IsCustomApiRegistration(CrmPluginRegistrationAttribute attribute)
        => attribute.Name is null && attribute.Message is not null && attribute.Stage is null;

    public static bool IsWorkflowActivityRegistration(CrmPluginRegistrationAttribute attribute)
        => attribute.Stage is null && attribute.Name is not null && attribute.FriendlyName is not null;

    public static bool IsPluginStepRegistration(CrmPluginRegistrationAttribute attribute)
        => attribute.Stage is not null;
}