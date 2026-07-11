using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PluginRegistration.Attributes;
using PluginRegistration.Core.Registration;

namespace PluginRegistration.Core.Config;

public sealed class ConfigScaffoldService
{
    private static readonly Regex AttributeRegex = new(
        @"\[CrmPluginRegistration\(([\s\S]+?)\)\]",
        RegexOptions.Compiled);

    private static readonly Regex ClassDeclarationRegex = new(
        @"(?:public\s+)?(?:sealed\s+)?class\s+(?<className>\w+)",
        RegexOptions.Compiled);

    private static readonly Regex NamespaceRegex = new(
        @"namespace\s+([\w.]+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex StageRegex = new(
        @"StageEnum\.(?<stage>\w+)",
        RegexOptions.Compiled);

    private static readonly Regex FirstStringArgRegex = new(
        @"""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex FriendlyNameRegex = new(
        @"FriendlyName\s*=\s*""([^""]+)""",
        RegexOptions.Compiled);

    private readonly ITrace _trace;

    public ConfigScaffoldService(ITrace trace)
    {
        _trace = trace;
    }

    public string Generate(
        string workingDirectory,
        IEnumerable<string> profiles,
        string assemblyPath = "bin/Release",
        string? solution = null,
        bool force = false)
    {
        var outputPath = Path.Combine(workingDirectory, "pluginregistration.json");
        if (File.Exists(outputPath) && !force)
        {
            throw new PluginRegistrationException(
                $"File already exists: {outputPath}. Use --force to overwrite.");
        }

        var config = CreateFromSource(workingDirectory, profiles, assemblyPath, solution);

        var json = JsonConvert.SerializeObject(config, Formatting.Indented, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy
                {
                    ProcessDictionaryKeys = false
                }
            }
        });

        File.WriteAllText(outputPath, json + Environment.NewLine);
        _trace.WriteLine("Created {0}", outputPath);
        return outputPath;
    }

    private static PluginRegistrationConfig CreateFromSource(
        string workingDirectory,
        IEnumerable<string> profiles,
        string assemblyPath,
        string? solution)
    {
        var stepNames = DiscoverStepNames(workingDirectory);
        var customApis = DiscoverCustomApis(workingDirectory);
        var profileArray = profiles.ToArray();

        var config = new PluginRegistrationConfig
        {
            Plugins =
            [
                new PluginDeployEntry
                {
                    Profile = string.Join(",", profileArray),
                    AssemblyPath = assemblyPath,
                    Solution = solution
                }
            ],
            Profiles = new Dictionary<string, ProfileSettings>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var profile in profileArray)
        {
            var settings = new ProfileSettings();

            foreach (var stepName in stepNames)
            {
                settings.StepOverrides[stepName] = new StepOverride
                {
                    UnSecureConfiguration = string.Empty
                };
            }

            foreach (var api in customApis)
            {
                settings.CustomApis.Add(new CustomApiDefinition
                {
                    UniqueName = api.UniqueName,
                    DisplayName = api.DisplayName,
                    PluginTypeName = api.PluginTypeName,
                    CreateIfMissing = string.Equals(profile, profileArray[0], StringComparison.OrdinalIgnoreCase),
                    BindingType = 0
                });
            }

            config.Profiles[profile] = settings;
        }

        return config;
    }

    private static HashSet<string> DiscoverStepNames(string workingDirectory)
        => DiscoverRegistrations(workingDirectory).StepNames;

    private static List<(string UniqueName, string DisplayName, string PluginTypeName)> DiscoverCustomApis(
        string workingDirectory)
        => DiscoverRegistrations(workingDirectory).CustomApis;

    private static (HashSet<string> StepNames, List<(string UniqueName, string DisplayName, string PluginTypeName)> CustomApis)
        DiscoverRegistrations(string workingDirectory)
    {
        var stepNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var customApis = new List<(string UniqueName, string DisplayName, string PluginTypeName)>();

        foreach (var file in EnumerateSourceFiles(workingDirectory))
        {
            var content = File.ReadAllText(file);
            var namespaceName = NamespaceRegex.Match(content) is { Success: true } namespaceMatch
                ? namespaceMatch.Groups[1].Value
                : string.Empty;

            foreach (Match match in AttributeRegex.Matches(content))
            {
                var className = ResolveClassForAttribute(content, match.Index + match.Length);
                if (string.IsNullOrWhiteSpace(className))
                {
                    continue;
                }

                var fullTypeName = string.IsNullOrWhiteSpace(namespaceName)
                    ? className
                    : $"{namespaceName}.{className}";

                var attributeBody = match.Groups[1].Value;
                var stageMatch = StageRegex.Match(attributeBody);
                if (stageMatch.Success)
                {
                    if (Enum.TryParse<StageEnum>(stageMatch.Groups["stage"].Value, out var stage))
                    {
                        stepNames.Add(PluginStepNameResolver.Resolve(fullTypeName, stage));
                    }

                    continue;
                }

                var uniqueNameMatch = FirstStringArgRegex.Match(attributeBody);
                if (!uniqueNameMatch.Success)
                {
                    continue;
                }

                var uniqueName = uniqueNameMatch.Groups[1].Value;
                var displayName = FriendlyNameRegex.Match(attributeBody) is { Success: true } friendlyNameMatch
                    ? friendlyNameMatch.Groups[1].Value
                    : uniqueName;

                customApis.Add((uniqueName, displayName, fullTypeName));
            }
        }

        return (stepNames, customApis
            .GroupBy(api => api.UniqueName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList());
    }

    private static string? ResolveClassForAttribute(string content, int attributeEndIndex)
    {
        foreach (Match match in ClassDeclarationRegex.Matches(content))
        {
            if (match.Index >= attributeEndIndex)
            {
                return match.Groups["className"].Value;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSourceFiles(string workingDirectory)
    {
        return Directory.EnumerateFiles(workingDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }
}