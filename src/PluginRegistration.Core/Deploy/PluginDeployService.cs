using Microsoft.Xrm.Sdk;
using PluginRegistration.Core.Config;
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

    public void Deploy(string workingDirectory, bool excludePluginSteps = false)
    {
        var config = PluginRegistrationConfig.Load(workingDirectory);
        var entries = config.GetPluginEntries();

        if (entries.Count == 0)
        {
            throw new PluginRegistrationException("No plugin deploy entries found in pluginregistration.json.");
        }

        var registrationService = new PluginRegistrationService(_service, _trace, config);
        var customApiService = new CustomApiRegistrationService(_service, _trace);

        foreach (var entry in entries)
        {
            _trace.WriteLine("Deploying plugins (solution: {0})", entry.Solution ?? "<none>");

            registrationService.SolutionUniqueName = entry.Solution;
            var skipSteps = excludePluginSteps || entry.ExcludePluginSteps;

            foreach (string packagePath in config.ResolvePackagePaths(entry))
            {
                registrationService.RegisterPluginPackage(packagePath, skipSteps);
            }

            foreach (var assemblyPath in config.ResolveAssemblyPaths(entry))
            {
                registrationService.RegisterPlugins(assemblyPath, skipSteps);
            }
        }

        if (config.CustomApis.Count > 0)
        {
            var solutionName = entries.LastOrDefault()?.Solution;
            customApiService.SolutionUniqueName = solutionName;
            customApiService.EnsureCustomApis(
                config.CustomApis.Where(definition => definition.CreateIfMissing));
        }
    }
}
