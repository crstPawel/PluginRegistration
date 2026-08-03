using Microsoft.Xrm.Sdk;
using PluginRegistration.Core.Registration;

namespace PluginRegistration.Core.Deploy;

public sealed class PluginDeployService
{
    private readonly IOrganizationService _service;
    private readonly ITrace _trace;

    public PluginDeployService(IOrganizationService service, ITrace trace)
    {
        _service = service;
        _trace = trace;
    }

    /// <summary>
    /// Deploys plugin NuGet packages from <paramref name="packagePath"/> under
    /// <paramref name="workingDirectory"/> and registers steps/Custom APIs from package content.
    /// </summary>
    /// <param name="workingDirectory">Base directory used to resolve relative package paths.</param>
    /// <param name="packagePath">Folder or file pattern for <c>*.nupkg</c> (e.g. <c>bin/Release</c>).</param>
    /// <param name="solutionUniqueName">Optional Dataverse solution unique name for components and publisher prefix.</param>
    /// <param name="excludePluginSteps">When true, upload package only (no steps/Custom APIs).</param>
    public void Deploy(
        string workingDirectory,
        string packagePath = "bin/Release",
        string? solutionUniqueName = null,
        bool excludePluginSteps = false)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
        {
            throw new PluginRegistrationException($"Working directory not found: {workingDirectory}");
        }

        _trace.WriteLine("Deploying plugins (solution: {0})", solutionUniqueName ?? "<none>");

        var solutionEnsure = new SolutionEnsureService(_service, _trace);
        solutionEnsure.EnsureExists(solutionUniqueName);

        var registrationService = new PluginRegistrationService(_service, _trace)
        {
            SolutionUniqueName = solutionUniqueName
        };

        var packagePaths = ResolvePackagePaths(workingDirectory, packagePath).ToList();
        if (packagePaths.Count == 0)
        {
            throw new PluginRegistrationException(
                $"No plugin packages (*.nupkg) found for packagePath '{packagePath}' under '{workingDirectory}'. " +
                "Build and pack the plugin project before deploy (e.g. dotnet pack -c Release).");
        }

        foreach (string resolvedPackagePath in packagePaths)
        {
            registrationService.RegisterPluginPackage(resolvedPackagePath, excludePluginSteps);
        }
    }

    /// <summary>
    /// Resolves .nupkg files under the working directory for a folder or pattern.
    /// </summary>
    public static IEnumerable<string> ResolvePackagePaths(string workingDirectory, string packagePath)
    {
        string pattern = string.IsNullOrWhiteSpace(packagePath) ? "bin/Release" : packagePath;

        var extension = Path.GetExtension(pattern);
        if (string.IsNullOrEmpty(extension))
        {
            pattern = Path.Combine(pattern, "*.nupkg");
        }

        var searchPattern = Path.GetFileName(pattern);
        string relativeDirectory = Path.GetDirectoryName(pattern) ?? string.Empty;
        string searchDirectory = Path.IsPathRooted(relativeDirectory)
            ? relativeDirectory
            : Path.Combine(workingDirectory, relativeDirectory);

        if (!Directory.Exists(searchDirectory))
        {
            throw new PluginRegistrationException($"Package path not found: {searchDirectory}");
        }

        return Directory.EnumerateFiles(searchDirectory, searchPattern, SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase)
                           && !path.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }
}
