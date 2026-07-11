namespace PluginRegistration.Core.EarlyBound;

internal static class EarlyBoundResourceLocator
{
    private const string ResourceFolderName = "DLaB.EarlyBoundGeneratorV2";
    private const string DictionaryFileName = "DLaB.Dictionary.txt";

    public static string EnsureResources(string baseDirectory)
    {
        var resourceDirectory = Path.Combine(baseDirectory, ResourceFolderName);
        Directory.CreateDirectory(resourceDirectory);

        var dictionaryPath = Path.Combine(resourceDirectory, DictionaryFileName);
        if (!File.Exists(dictionaryPath))
        {
            var sourcePath = FindDictionarySource(baseDirectory);
            if (sourcePath is null)
            {
                throw new PluginRegistrationException(
                    $"Unable to locate {DictionaryFileName}. Rebuild the tool or reinstall the DLaB.Xrm.EarlyBoundGeneratorV2.Api package.");
            }

            File.Copy(sourcePath, dictionaryPath, overwrite: true);
        }

        return baseDirectory;
    }

    private static string? FindDictionarySource(string baseDirectory)
    {
        var candidates = new[]
        {
            Path.Combine(baseDirectory, DictionaryFileName),
            Path.Combine(baseDirectory, ResourceFolderName, DictionaryFileName),
            Path.Combine(baseDirectory, "bin", ResourceFolderName, DictionaryFileName)
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.GetName().Name?.StartsWith("DLaB.", StringComparison.Ordinal) ?? true)
            {
                continue;
            }

            var assemblyDirectory = Path.GetDirectoryName(assembly.Location);
            if (string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                continue;
            }

            var adjacentDictionary = Path.Combine(assemblyDirectory, DictionaryFileName);
            if (File.Exists(adjacentDictionary))
            {
                return adjacentDictionary;
            }
        }

        var nuGetRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        var packageRoot = Path.Combine(nuGetRoot, "dlab.xrm.earlyboundgeneratorv2.api");
        if (!Directory.Exists(packageRoot))
        {
            return null;
        }

        var packageDictionary = Directory
            .EnumerateFiles(packageRoot, DictionaryFileName, SearchOption.AllDirectories)
            .OrderByDescending(path => path.Contains("net10.0", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(path => path.Contains("lib", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        return packageDictionary;
    }
}