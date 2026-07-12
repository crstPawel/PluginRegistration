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
        => ResolveArtifactPaths(entry.AssemblyPath, "*.dll", "Assembly");

    public IEnumerable<string> ResolvePackagePaths(PluginDeployEntry entry)
    {
        if (String.IsNullOrWhiteSpace(entry.PackagePath))
        {
            return [];
        }

        return ResolveArtifactPaths(entry.PackagePath, "*.nupkg", "Package");
    }

    private IEnumerable<string> ResolveArtifactPaths(string? pattern, string defaultSearchPattern, string artifactLabel)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            pattern = "bin/Release";
        }

        var extension = Path.GetExtension(pattern);
        if (string.IsNullOrEmpty(extension))
        {
            pattern = Path.Combine(pattern, defaultSearchPattern);
        }

        var searchPattern = Path.GetFileName(pattern);
        string searchDirectory = Path.Combine(FilePath, Path.GetDirectoryName(pattern) ?? String.Empty);

        if (!Directory.Exists(searchDirectory))
        {
            throw new PluginRegistrationException($"{artifactLabel} path not found: {searchDirectory}");
        }

        return Directory.EnumerateFiles(searchDirectory, searchPattern, SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class PluginDeployEntry
{
    public string? Solution { get; set; }
    public string AssemblyPath { get; set; } = "bin/Release";
    public string? PackagePath { get; set; }
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
