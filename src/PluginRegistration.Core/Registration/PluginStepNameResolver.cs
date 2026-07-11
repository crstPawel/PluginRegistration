using PluginRegistration.Attributes;

namespace PluginRegistration.Core.Registration;

public static class PluginStepNameResolver
{
    public static string Resolve(Type pluginType, StageEnum stage)
        => Resolve(pluginType.FullName!, stage);

    public static string Resolve(string pluginTypeFullName, StageEnum stage)
        => $"{pluginTypeFullName}.{stage}";

    public static CrmPluginRegistrationAttribute ApplyStepName(
        Type pluginType,
        CrmPluginRegistrationAttribute attribute)
    {
        if (attribute.Stage is not null && string.IsNullOrWhiteSpace(attribute.Name))
        {
            attribute.Name = Resolve(pluginType, attribute.Stage.Value);
        }

        return attribute;
    }
}