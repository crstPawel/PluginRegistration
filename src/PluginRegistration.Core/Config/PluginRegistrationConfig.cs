using Newtonsoft.Json;

namespace PluginRegistration.Core.Config;

public sealed class PluginRegistrationConfig
{
    public List<PluginDeployEntry> Plugins { get; set; } = [];

    /// <summary>
    /// Step overrides keyed by step name or step Id (GUID).
    /// </summary>
    public Dictionary<string, StepOverride> StepOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Custom API definitions for deployment.
    /// </summary>
    public List<CustomApiDefinition> CustomApis { get; set; } = [];

    [JsonIgnore]
    public string FilePath { get; set; } = string.Empty;

    public static PluginRegistrationConfig Load(string folder)
    {
        var file = Path.Combine(folder, "pluginregistration.json");
        if (!File.Exists(file))
        {
            throw new PluginRegistrationException($"Configuration file not found: {file}");
        }

        var json = File.ReadAllText(file);
        var config = JsonConvert.DeserializeObject<PluginRegistrationConfig>(json)
            ?? throw new PluginRegistrationException("Invalid pluginregistration.json content.");

        config.FilePath = folder;
        return config;
    }

    public IReadOnlyList<PluginDeployEntry> GetPluginEntries()
        => Plugins;

    public IEnumerable<string> ResolveAssemblyPaths(PluginDeployEntry entry)
    {
        var pattern = entry.AssemblyPath;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            pattern = "bin/Release";
        }

        var extension = Path.GetExtension(pattern);
        if (string.IsNullOrEmpty(extension))
        {
            pattern = Path.Combine(pattern, "*.dll");
        }

        var directory = FilePath;
        var searchPattern = Path.GetFileName(pattern);
        var searchDirectory = Path.Combine(directory, Path.GetDirectoryName(pattern) ?? string.Empty);

        if (!Directory.Exists(searchDirectory))
        {
            throw new PluginRegistrationException($"Assembly path not found: {searchDirectory}");
        }

        // Search recursively to support modern SDK output layouts (e.g. bin/Release/net462/*.dll, bin/Release/net8.0/* etc.)
        // RegisterPlugins will filter out non-plugin assemblies and dependencies.
        return Directory.EnumerateFiles(searchDirectory, searchPattern, SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class PluginDeployEntry
{
    public string? Solution { get; set; }
    public string AssemblyPath { get; set; } = "bin/Release";
    public bool ExcludePluginSteps { get; set; }
}

public sealed class StepOverride
{
    public string? UnSecureConfiguration { get; set; }
    public string? SecureConfiguration { get; set; }
    public string? Description { get; set; }
}

public sealed class CustomApiDefinition
{
    public string UniqueName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PluginTypeName { get; set; }
    public bool CreateIfMissing { get; set; }
    public bool IsFunction { get; set; }
    public bool IsPrivate { get; set; }
    public int BindingType { get; set; }
    public string? BoundEntityLogicalName { get; set; }
    public int AllowedCustomProcessingStepType { get; set; }
    public List<CustomApiParameterDefinition> RequestParameters { get; set; } = [];
    public List<CustomApiParameterDefinition> ResponseProperties { get; set; } = [];
}

public sealed class CustomApiParameterDefinition
{
    public string UniqueName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Type { get; set; }
    public bool IsRequired { get; set; }
    public string? EntityLogicalName { get; set; }
}