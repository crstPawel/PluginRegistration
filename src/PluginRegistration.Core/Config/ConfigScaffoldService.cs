using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PluginRegistration.Attributes;
using PluginRegistration.Core.Registration;

namespace PluginRegistration.Core.Config;

public sealed class ConfigScaffoldService
{
    private static readonly Regex PluginRegistrationRegex = new(
        @"\[PluginRegistration\(([\s\S]+?)\)\]|\[CrmPluginRegistration\(([\s\S]+?)\)\]",
        RegexOptions.Compiled);

    private static readonly Regex CustomApiRegistrationRegex = new(
        @"\[CustomApiRegistration\(([\s\S]+?)\)\]",
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

        var config = CreateFromSource(workingDirectory, assemblyPath, solution);

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
        string assemblyPath,
        string? solution)
    {
        var stepNames = DiscoverStepNames(workingDirectory);
        var customApis = DiscoverCustomApis(workingDirectory);

        var config = new PluginRegistrationConfig
        {
            Plugins =
            [
                new PluginDeployEntry
                {
                    AssemblyPath = assemblyPath,
                    Solution = solution
                }
            ]
        };

        foreach (var stepName in stepNames)
        {
            config.StepOverrides[stepName] = new StepOverride
            {
                UnSecureConfiguration = string.Empty
            };
        }

        foreach (var api in customApis)
        {
            config.CustomApis.Add(new CustomApiDefinition
            {
                UniqueName = api.UniqueName,
                DisplayName = api.DisplayName,
                PluginTypeName = api.PluginTypeName,
                CreateIfMissing = true,
                BindingType = 0
            });
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

            foreach (Match match in PluginRegistrationRegex.Matches(content))
            {
                var className = ResolveClassForAttribute(content, match.Index + match.Length);
                if (string.IsNullOrWhiteSpace(className))
                {
                    continue;
                }

                var fullTypeName = string.IsNullOrWhiteSpace(namespaceName)
                    ? className
                    : $"{namespaceName}.{className}";

                var attributeBody = !string.IsNullOrEmpty(match.Groups[1].Value)
                    ? match.Groups[1].Value
                    : match.Groups[2].Value;

                var stageMatch = StageRegex.Match(attributeBody);
                if (!stageMatch.Success
                    || !Enum.TryParse<StageEnum>(stageMatch.Groups["stage"].Value, out var stage))
                {
                    continue;
                }

                stepNames.Add(PluginStepNameResolver.Resolve(fullTypeName, stage));
            }

            foreach (Match match in CustomApiRegistrationRegex.Matches(content))
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