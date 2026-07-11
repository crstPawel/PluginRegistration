using System.Text.RegularExpressions;
using PluginRegistration.Attributes;
using PluginRegistration.Core.Config;

namespace PluginRegistration.Core.Registration;

public sealed class EnvironmentConfigurationResolver
{
    private static readonly Regex EnvVarPattern = new(@"\$\{([^}]+)\}", RegexOptions.Compiled);

    private readonly ProfileSettings? _profileSettings;

    public EnvironmentConfigurationResolver(ProfileSettings? profileSettings)
    {
        _profileSettings = profileSettings;
    }

    public CustomApiDefinition? GetCustomApiOverride(string uniqueName)
    {
        if (_profileSettings is null)
        {
            return null;
        }

        return _profileSettings.CustomApis.FirstOrDefault(definition =>
            string.Equals(definition.UniqueName, uniqueName, StringComparison.OrdinalIgnoreCase));
    }

    public CrmPluginRegistrationAttribute ApplyProfileOverrides(CrmPluginRegistrationAttribute attribute)
    {
        if (_profileSettings is null || attribute.Name is null)
        {
            return ApplyEnvironmentVariables(attribute);
        }

        StepOverride? stepOverride = null;
        if (!string.IsNullOrWhiteSpace(attribute.Id)
            && _profileSettings.StepOverrides.TryGetValue(attribute.Id, out var byId))
        {
            stepOverride = byId;
        }
        else if (!string.IsNullOrWhiteSpace(attribute.Name)
            && _profileSettings.StepOverrides.TryGetValue(attribute.Name, out var byName))
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

        if (stepOverride.Description is not null)
        {
            attribute.Description = ExpandEnvironmentVariables(stepOverride.Description);
        }

        return attribute;
    }

    private static CrmPluginRegistrationAttribute ApplyEnvironmentVariables(CrmPluginRegistrationAttribute attribute)
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