using System.Text.RegularExpressions;
using PluginRegistration.Attributes;
using PluginRegistration.Core.Config;

namespace PluginRegistration.Core.Registration;

public sealed class EnvironmentConfigurationResolver
{
    private static readonly Regex EnvVarPattern = new(@"\$\{([^}]+)\}", RegexOptions.Compiled);

    private readonly Dictionary<string, StepOverride> _stepOverrides;
    private readonly List<CustomApiDefinition> _customApis;

    public EnvironmentConfigurationResolver(
        Dictionary<string, StepOverride> stepOverrides,
        List<CustomApiDefinition> customApis)
    {
        _stepOverrides = stepOverrides;
        _customApis = customApis;
    }

    public CustomApiDefinition? GetCustomApiOverride(string uniqueName)
        => _customApis.FirstOrDefault(definition =>
            string.Equals(definition.UniqueName, uniqueName, StringComparison.OrdinalIgnoreCase));

    public PluginRegistrationAttribute ApplyStepOverrides(PluginRegistrationAttribute attribute)
    {
        if (attribute.Name is null)
        {
            return ApplyEnvironmentVariables(attribute);
        }

        StepOverride? stepOverride = null;
        if (!string.IsNullOrWhiteSpace(attribute.Id)
            && _stepOverrides.TryGetValue(attribute.Id, out var byId))
        {
            stepOverride = byId;
        }
        else if (!string.IsNullOrWhiteSpace(attribute.Name)
            && _stepOverrides.TryGetValue(attribute.Name, out var byName))
        {
            stepOverride = byName;
        }

        if (stepOverride is null)
        {
            return ApplyEnvironmentVariables(attribute);
        }

        if (stepOverride.UnSecureConfiguration is not null)
        {
            attribute.UnSecureConfiguration = ExpandEnvironmentVariables(stepOverride.UnSecureConfiguration);
        }

        if (stepOverride.SecureConfiguration is not null)
        {
            attribute.SecureConfiguration = ExpandEnvironmentVariables(stepOverride.SecureConfiguration);
        }

        return attribute;
    }

    private static PluginRegistrationAttribute ApplyEnvironmentVariables(PluginRegistrationAttribute attribute)
    {
        if (attribute.UnSecureConfiguration is not null)
        {
            attribute.UnSecureConfiguration = ExpandEnvironmentVariables(attribute.UnSecureConfiguration);
        }

        if (attribute.SecureConfiguration is not null)
        {
            attribute.SecureConfiguration = ExpandEnvironmentVariables(attribute.SecureConfiguration);
        }

        return attribute;
    }

    public static string ExpandEnvironmentVariables(string value)
    {
        return EnvVarPattern.Replace(value, match =>
        {
            var variableName = match.Groups[1].Value;
            return Environment.GetEnvironmentVariable(variableName)
                ?? throw new PluginRegistrationException($"Environment variable '{variableName}' is not set.");
        });
    }
}