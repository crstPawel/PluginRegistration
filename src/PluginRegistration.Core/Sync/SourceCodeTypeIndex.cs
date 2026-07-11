using System.Text.RegularExpressions;

namespace PluginRegistration.Core.Sync;

/// <summary>
/// Indexes class inheritance across all source files to detect plugins inheriting from custom base classes.
/// </summary>
public sealed class SourceCodeTypeIndex
{
    private static readonly Regex NamespaceRegex = new(@"namespace\s+(?<ns>[\w.]+)", RegexOptions.Compiled);
    private static readonly Regex ClassDeclarationRegex = new(
        @"public\s+(?:sealed\s+|abstract\s+)*class\s+(?<class>\w+)(?:\s*:\s*(?<base>[\w]+))?",
        RegexOptions.Compiled);

    private static readonly HashSet<string> PluginRootBases = new(StringComparer.Ordinal)
    {
        "IPlugin",
        "PluginBase",
        "Plugin"
    };

    private static readonly HashSet<string> WorkflowRootBases = new(StringComparer.Ordinal)
    {
        "CodeActivity",
        "WorkFlowActivityBase"
    };

    private readonly Dictionary<string, TypeInfo> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _pluginTypes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _workflowTypes = new(StringComparer.Ordinal);

    public static SourceCodeTypeIndex Build(string sourceDirectory)
    {
        var index = new SourceCodeTypeIndex();
        index.Scan(sourceDirectory);
        index.ResolveInheritance();
        return index;
    }

    public IReadOnlyCollection<string> GetPluginTypesInFile(string filePath)
        => _pluginTypes.TryGetValue(filePath, out var types) ? types : [];

    public IReadOnlyCollection<string> GetWorkflowTypesInFile(string filePath)
        => _workflowTypes.TryGetValue(filePath, out var types) ? types : [];

    public bool HasPluginOrWorkflowTypes(string filePath)
        => GetPluginTypesInFile(filePath).Count > 0 || GetWorkflowTypesInFile(filePath).Count > 0;

    public int PluginTypeCount => _pluginTypes.Values.Sum(types => types.Count);

    public int WorkflowTypeCount => _workflowTypes.Values.Sum(types => types.Count);

    private void Scan(string sourceDirectory)
    {
        foreach (var file in EnumerateSourceFiles(sourceDirectory))
        {
            var content = File.ReadAllText(file);
            var namespaceMatch = NamespaceRegex.Match(content);
            var namespaceName = namespaceMatch.Success ? namespaceMatch.Groups["ns"].Value : string.Empty;

            foreach (Match match in ClassDeclarationRegex.Matches(content))
            {
                var className = match.Groups["class"].Value;
                var fullName = string.IsNullOrEmpty(namespaceName) ? className : $"{namespaceName}.{className}";
                var baseName = match.Groups["base"].Success ? match.Groups["base"].Value : null;

                _types[fullName] = new TypeInfo(file, className, namespaceName, baseName, match.Value);
            }
        }
    }

    private void ResolveInheritance()
    {
        foreach (var (fullName, info) in _types)
        {
            if (IsPluginType(fullName))
            {
                AddToFileIndex(_pluginTypes, info.FilePath, fullName);
            }
            else if (IsWorkflowType(fullName))
            {
                AddToFileIndex(_workflowTypes, info.FilePath, fullName);
            }
        }
    }

    private bool IsPluginType(string fullName)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = fullName;

        while (!string.IsNullOrEmpty(current) && visited.Add(current))
        {
            if (!_types.TryGetValue(current, out var info))
            {
                if (_types.Values.Any(t => t.ClassName == current))
                {
                    current = _types.Values.First(t => t.ClassName == current).FullName;
                    continue;
                }

                return PluginRootBases.Contains(current);
            }

            if (info.BaseName is null)
            {
                return false;
            }

            if (PluginRootBases.Contains(info.BaseName))
            {
                return true;
            }

            current = ResolveBaseFullName(info);
        }

        return false;
    }

    private bool IsWorkflowType(string fullName)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = fullName;

        while (!string.IsNullOrEmpty(current) && visited.Add(current))
        {
            if (!_types.TryGetValue(current, out var info))
            {
                if (_types.Values.Any(t => t.ClassName == current))
                {
                    current = _types.Values.First(t => t.ClassName == current).FullName;
                    continue;
                }

                return WorkflowRootBases.Contains(current);
            }

            if (info.BaseName is null)
            {
                return false;
            }

            if (WorkflowRootBases.Contains(info.BaseName))
            {
                return true;
            }

            current = ResolveBaseFullName(info);
        }

        return false;
    }

    private string? ResolveBaseFullName(TypeInfo info)
    {
        if (string.IsNullOrEmpty(info.BaseName))
        {
            return null;
        }

        var candidate = string.IsNullOrEmpty(info.NamespaceName)
            ? info.BaseName
            : $"{info.NamespaceName}.{info.BaseName}";

        if (_types.ContainsKey(candidate))
        {
            return candidate;
        }

        var matches = _types.Values.Where(t => t.ClassName == info.BaseName).ToList();
        return matches.Count == 1 ? matches[0].FullName : info.BaseName;
    }

    private static void AddToFileIndex(Dictionary<string, HashSet<string>> index, string filePath, string fullName)
    {
        if (!index.TryGetValue(filePath, out var types))
        {
            types = new HashSet<string>(StringComparer.Ordinal);
            index[filePath] = types;
        }

        types.Add(fullName);
    }

    public string? GetClassDeclaration(string fullName)
        => _types.TryGetValue(fullName, out var info) ? info.Declaration : null;

    private static IEnumerable<string> EnumerateSourceFiles(string sourceDirectory)
    {
        return Directory.EnumerateFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    private sealed class TypeInfo(string file, string className, string namespaceName, string? baseName, string declaration)
    {
        public string FilePath { get; } = file;
        public string ClassName { get; } = className;
        public string NamespaceName { get; } = namespaceName;
        public string FullName => string.IsNullOrEmpty(NamespaceName) ? ClassName : $"{NamespaceName}.{ClassName}";
        public string? BaseName { get; } = baseName;
        public string Declaration { get; } = declaration;
    }
}