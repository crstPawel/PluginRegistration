using DLaB.EarlyBoundGeneratorV2;
using DLaB.EarlyBoundGeneratorV2.Settings;
using Microsoft.Xrm.Sdk;

namespace PluginRegistration.Core.EarlyBound;

/// <summary>
/// Generates early-bound Dataverse types using DLaB Early Bound Generator V2 (PAC ModelBuilder).
/// </summary>
public sealed class EarlyBoundGeneratorService
{
    public const string DefaultConfigFileName = "earlyboundgenerator.xml";
    public const string DefaultOutputFolderName = "EarlyBound";

    private readonly IOrganizationService? _service;
    private readonly ITrace _trace;

    public EarlyBoundGeneratorService(IOrganizationService? service, ITrace trace)
    {
        _service = service;
        _trace = trace;
    }

    public void Generate(EarlyBoundGenerationRequest request)
    {
        var workingDirectory = Path.GetFullPath(request.WorkingDirectory);
        Directory.CreateDirectory(workingDirectory);

        var configPath = ResolveConfigPath(workingDirectory, request);
        var useJsonConfig = ShouldUseJsonConfig(request, configPath);
        var outputDirectory = Path.GetFullPath(
            request.OutputDirectory ?? Path.Combine(workingDirectory, DefaultOutputFolderName));
        var pluginPath = EarlyBoundResourceLocator.EnsureResources(AppContext.BaseDirectory);

        if (request.InitConfigOnly)
        {
            if (useJsonConfig)
            {
                WriteDefaultJsonConfig(configPath, request, outputDirectory, pluginPath, ShouldForceConfigOverwrite(request));
            }
            else
            {
                WriteDefaultXmlConfig(configPath, request, outputDirectory, pluginPath, ShouldForceConfigOverwrite(request));
            }

            return;
        }

        EarlyBoundJsonConfig? jsonConfig = null;
        if (useJsonConfig)
        {
            jsonConfig = EarlyBoundJsonConfig.Load(configPath);
            jsonConfig.ApplyToRequest(request, workingDirectory);
            outputDirectory = Path.GetFullPath(
                request.OutputDirectory ?? Path.Combine(workingDirectory, DefaultOutputFolderName));
            _trace.WriteLine("Loaded early bound JSON config: {0}", configPath);
        }

        var config = useJsonConfig
            ? CreateDefaultConfig()
            : File.Exists(configPath)
                ? EarlyBoundGeneratorConfig.Load(configPath)
                : CreateDefaultConfig();

        if (jsonConfig is not null)
        {
            EarlyBoundJsonConfigApplier.Apply(jsonConfig, config);
        }

        ApplyRequestOverrides(config, request, outputDirectory, pluginPath);

        if (request.OverwriteExistingFiles == true)
        {
            EarlyBoundOutputFilePreparer.PrepareForOverwrite(outputDirectory, _trace);
        }

        // Do not generate builderSettings.json when using this tool for Early Bound generation.
        config.UpdateBuilderSettingsJson = false;

        var logic = new Logic(config);

        _trace.WriteLine("Generating early bound types to {0}", outputDirectory);
        _trace.WriteLine("Namespace: {0}", config.Namespace);
        _trace.WriteLine("Service context: {0}", config.ServiceContextName);

        if (_service is null)
        {
            throw new PluginRegistrationException("Dataverse connection is required for early bound generation.");
        }

        if (!logic.Create(_service))
        {
            throw new PluginRegistrationException(
                "Early bound generation failed. Review the log output above for details.");
        }

        _trace.WriteLine("Early bound generation completed successfully.");
    }

    private void WriteDefaultXmlConfig(
        string configPath,
        EarlyBoundGenerationRequest request,
        string outputDirectory,
        string pluginPath,
        bool force)
    {
        if (File.Exists(configPath) && !force)
        {
            throw new PluginRegistrationException(
                $"Config file already exists: {configPath}. Use --force or --overwrite to replace it.");
        }

        var config = CreateDefaultConfig();
        ApplyRequestOverrides(config, request, outputDirectory, pluginPath);
        config.Save(configPath);
        _trace.WriteLine("Created early bound XML config: {0}", configPath);
    }

    private void WriteDefaultJsonConfig(
        string configPath,
        EarlyBoundGenerationRequest request,
        string outputDirectory,
        string pluginPath,
        bool force)
    {
        if (File.Exists(configPath) && !force)
        {
            throw new PluginRegistrationException(
                $"Config file already exists: {configPath}. Use --force or --overwrite to replace it.");
        }

        var config = CreateDefaultConfig();
        ApplyRequestOverrides(config, request, outputDirectory, pluginPath);
        var jsonConfig = EarlyBoundJsonConfigApplier.FromGeneratorConfig(config, outputDirectory, request.WorkingDirectory);
        jsonConfig.Save(configPath);
        _trace.WriteLine("Created early bound JSON config: {0}", configPath);
    }

    private static EarlyBoundGeneratorConfig CreateDefaultConfig()
    {
        var config = EarlyBoundGeneratorConfig.GetDefault();
        config.ExtensionConfig.EntitiesWhitelist = null;
        config.ExtensionConfig.EntityPrefixesWhitelist = null;
        config.ExtensionConfig.ActionsWhitelist = null;
        config.ExtensionConfig.ActionPrefixesWhitelist = null;
        config.UpdateBuilderSettingsJson = false;
        return config;
    }

    private static string ResolveConfigPath(string workingDirectory, EarlyBoundGenerationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ConfigFilePath))
        {
            return Path.IsPathRooted(request.ConfigFilePath)
                ? request.ConfigFilePath
                : Path.Combine(workingDirectory, request.ConfigFilePath);
        }

        if (request.UseJsonConfig)
        {
            return Path.Combine(workingDirectory, EarlyBoundJsonConfig.DefaultFileName);
        }

        return Path.Combine(workingDirectory, DefaultConfigFileName);
    }

    private static bool ShouldUseJsonConfig(EarlyBoundGenerationRequest request, string configPath)
    {
        if (request.UseJsonConfig)
        {
            return true;
        }

        return configPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyRequestOverrides(
        EarlyBoundGeneratorConfig config,
        EarlyBoundGenerationRequest request,
        string outputDirectory,
        string pluginPath)
    {
        Directory.CreateDirectory(outputDirectory);

        config.RootPath = outputDirectory;
        config.AudibleCompletionNotification = false;
        config.ExtensionConfig.XrmToolBoxPluginPath = pluginPath;
        config.ExtensionConfig.AddNewFilesToProject = false;
        config.ExtensionConfig.CamelCaseNamesDictionaryRelativePath =
            NormalizeResourcePath("DLaB.EarlyBoundGeneratorV2/DLaB.Dictionary.txt");
        config.ExtensionConfig.TransliterationRelativePath =
            NormalizeResourcePath("DLaB.EarlyBoundGeneratorV2/alphabets");

        if (!string.IsNullOrWhiteSpace(request.Namespace))
        {
            config.Namespace = request.Namespace;
        }

        if (!string.IsNullOrWhiteSpace(request.ServiceContextName))
        {
            config.ServiceContextName = request.ServiceContextName;
        }

        if (!string.IsNullOrWhiteSpace(request.EntitiesWhitelist))
        {
            config.ExtensionConfig.EntitiesWhitelist = request.EntitiesWhitelist;
        }

        if (request.GenerateMessages.HasValue)
        {
            config.GenerateMessages = request.GenerateMessages.Value;
        }

        if (request.GenerateGlobalOptionSets.HasValue)
        {
            config.ExtensionConfig.GenerateGlobalOptionSets = request.GenerateGlobalOptionSets.Value;
        }

        if (request.OverwriteExistingFiles == true)
        {
            config.ExtensionConfig.DeleteFilesFromOutputFolders = true;
        }
    }

    private static bool ShouldForceConfigOverwrite(EarlyBoundGenerationRequest request)
        => request.ForceInitConfig || request.OverwriteExistingFiles == true;

    private static string NormalizeResourcePath(string relativePath)
        => relativePath.Replace('\\', Path.DirectorySeparatorChar);
}