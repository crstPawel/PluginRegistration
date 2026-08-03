using DLaB.EarlyBoundGeneratorV2;
using DLaB.EarlyBoundGeneratorV2.Settings;
using Microsoft.Xrm.Sdk;

namespace PluginRegistration.Core.EarlyBound;

/// <summary>
/// Generates early-bound Dataverse types using DLaB Early Bound Generator V2 (PAC ModelBuilder).
/// Configuration is the native DLaB <c>earlyboundgenerator.xml</c> (<see cref="EarlyBoundGeneratorConfig"/>).
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
        var pluginPath = EarlyBoundResourceLocator.EnsureResources(AppContext.BaseDirectory);

        if (request.InitConfigOnly)
        {
            WriteDefaultXmlConfig(configPath, request, pluginPath, ShouldForceConfigOverwrite(request));
            return;
        }

        var config = LoadConfig(configPath);
        ApplyRequestOverrides(config, request, pluginPath);

        var outputDirectory = ResolveOutputDirectory(config, request, workingDirectory);
        config.RootPath = outputDirectory;
        Directory.CreateDirectory(outputDirectory);

        if (request.OverwriteExistingFiles == true
            || config.ExtensionConfig.DeleteFilesFromOutputFolders)
        {
            EarlyBoundOutputFilePreparer.PrepareForOverwrite(outputDirectory, _trace);
        }

        // This CLI hosts generation; do not emit PAC builderSettings.json as a side effect.
        config.UpdateBuilderSettingsJson = false;

        var logic = new Logic(config);

        _trace.WriteLine("Using DLaB EBG V2 config: {0}", configPath);
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
        string pluginPath,
        bool force)
    {
        if (File.Exists(configPath) && !force)
        {
            throw new PluginRegistrationException(
                $"Config file already exists: {configPath}. Use --force or --overwrite to replace it.");
        }

        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Full native DLaB EarlyBoundGeneratorConfig defaults (complete ExtensionConfig schema).
        var config = EarlyBoundGeneratorConfig.GetDefault();
        ApplyToolHostDefaults(config, pluginPath);

        // Optional CLI overrides on scaffold so the XML starts with useful values.
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

        if (!string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            config.RootPath = Path.GetFullPath(request.OutputDirectory);
        }

        config.Save(configPath);
        _trace.WriteLine("Created DLaB Early Bound Generator V2 XML config: {0}", configPath);
    }

    private static EarlyBoundGeneratorConfig LoadConfig(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new PluginRegistrationException(
                $"DLaB early bound config not found: {configPath}. " +
                "Run 'pluginreg earlybound --init-config' to create earlyboundgenerator.xml, " +
                "or pass --config with a path to an existing DLaB EBG V2 XML file.");
        }

        return EarlyBoundGeneratorConfig.Load(configPath);
    }

    private static string ResolveConfigPath(string workingDirectory, EarlyBoundGenerationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ConfigFilePath))
        {
            return Path.IsPathRooted(request.ConfigFilePath)
                ? request.ConfigFilePath
                : Path.Combine(workingDirectory, request.ConfigFilePath);
        }

        return Path.Combine(workingDirectory, DefaultConfigFileName);
    }

    private static string ResolveOutputDirectory(
        EarlyBoundGeneratorConfig config,
        EarlyBoundGenerationRequest request,
        string workingDirectory)
    {
        if (!string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            return Path.GetFullPath(request.OutputDirectory);
        }

        if (!string.IsNullOrWhiteSpace(config.RootPath))
        {
            return Path.IsPathRooted(config.RootPath)
                ? Path.GetFullPath(config.RootPath)
                : Path.GetFullPath(Path.Combine(workingDirectory, config.RootPath));
        }

        return Path.GetFullPath(Path.Combine(workingDirectory, DefaultOutputFolderName));
    }

    /// <summary>
    /// Applies only values explicitly supplied on the CLI request. All other settings come from XML.
    /// </summary>
    private static void ApplyRequestOverrides(
        EarlyBoundGeneratorConfig config,
        EarlyBoundGenerationRequest request,
        string pluginPath)
    {
        ApplyToolHostDefaults(config, pluginPath);

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

    /// <summary>
    /// Host-level defaults required to run DLaB EBG V2 under this CLI (not schema changes).
    /// Does not clear or rewrite user ExtensionConfig filters / generation flags from XML.
    /// </summary>
    private static void ApplyToolHostDefaults(EarlyBoundGeneratorConfig config, string pluginPath)
    {
        config.AudibleCompletionNotification = false;
        config.ExtensionConfig.XrmToolBoxPluginPath = pluginPath;
        config.ExtensionConfig.AddNewFilesToProject = false;

        // Ensure resource paths resolve next to the tool binaries when XML uses DLaB defaults.
        if (string.IsNullOrWhiteSpace(config.ExtensionConfig.CamelCaseNamesDictionaryRelativePath))
        {
            config.ExtensionConfig.CamelCaseNamesDictionaryRelativePath =
                NormalizeResourcePath("DLaB.EarlyBoundGeneratorV2/DLaB.Dictionary.txt");
        }

        if (string.IsNullOrWhiteSpace(config.ExtensionConfig.TransliterationRelativePath))
        {
            config.ExtensionConfig.TransliterationRelativePath =
                NormalizeResourcePath("DLaB.EarlyBoundGeneratorV2/alphabets");
        }
    }

    private static bool ShouldForceConfigOverwrite(EarlyBoundGenerationRequest request)
        => request.ForceInitConfig || request.OverwriteExistingFiles == true;

    private static string NormalizeResourcePath(string relativePath)
        => relativePath.Replace('\\', Path.DirectorySeparatorChar);
}
