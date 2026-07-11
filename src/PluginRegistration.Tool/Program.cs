using Microsoft.Xrm.Sdk;
using PluginRegistration.Core;
using PluginRegistration.Core.Connection;
using PluginRegistration.Core.Config;
using PluginRegistration.Core.Deploy;
using PluginRegistration.Core.EarlyBound;
using PluginRegistration.Core.Sync;
using PluginRegistration.Tool.Cli;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

var root = new RootCommand("Dataverse plugin registration tool for Azure DevOps pipelines.");

var pathOption = new Option<DirectoryInfo>(
    aliases: ["--path", "-p"],
    description: "Working directory containing pluginregistration.json and plugin assemblies.")
{
    IsRequired = false
};

var profileOption = new Option<string?>(
    aliases: ["--profile", "-pr"],
    description: "Environment profile name (dev, test, prod).");

var connectionOption = new Option<string?>(
    aliases: ["--connection", "-c"],
    description: "Dataverse connection string. If omitted, DATAVERSE_* environment variables are used.");

var excludeStepsOption = new Option<bool>(
    aliases: ["--exclude-steps"],
    description: "Upload assemblies only, skip plugin step registration.");

var workflowOption = new Option<bool>(
    aliases: ["--workflow"],
    description: "Also register custom workflow activities.");

var classRegexOption = new Option<string?>(
    aliases: ["--class-regex"],
    description: "Custom regex for detecting plugin classes during sync.");

var deployCommand = new Command("deploy", "Deploy plugin assemblies and register steps for the selected profile.");
deployCommand.AddOption(pathOption);
deployCommand.AddOption(profileOption);
deployCommand.AddOption(connectionOption);
deployCommand.AddOption(excludeStepsOption);
deployCommand.AddOption(workflowOption);
CommandValidators.AddDeployValidators(deployCommand, pathOption, profileOption, connectionOption);
deployCommand.SetHandler(DeployAsync, pathOption, profileOption, connectionOption, excludeStepsOption, workflowOption);

var syncCommand = new Command("sync", "Download plugin step metadata from Dataverse and update source code attributes.");
syncCommand.AddOption(pathOption);
syncCommand.AddOption(connectionOption);
syncCommand.AddOption(classRegexOption);
CommandValidators.AddSyncValidators(syncCommand, pathOption, connectionOption);
syncCommand.SetHandler(SyncAsync, pathOption, connectionOption, classRegexOption);

var whoamiCommand = new Command("whoami", "Validate Dataverse connection.");
whoamiCommand.AddOption(connectionOption);
CommandValidators.AddWhoAmIValidators(whoamiCommand, connectionOption);
whoamiCommand.SetHandler(WhoAmIAsync, connectionOption);

var profilesOption = new Option<string>(
    aliases: ["--profiles"],
    getDefaultValue: () => "dev,test,prod",
    description: "Comma-separated profile names for generated config.");

var assemblyPathOption = new Option<string>(
    aliases: ["--assembly-path"],
    getDefaultValue: () => "bin/Release",
    description: "Assembly output path written into pluginregistration.json.");

var solutionOption = new Option<string?>(
    aliases: ["--solution"],
    description: "Solution unique name to add plugin components to.");

var forceOption = new Option<bool>(
    aliases: ["--force"],
    description: "Overwrite existing pluginregistration.json.");

var initCommand = new Command("init", "Generate pluginregistration.json from source code.");
initCommand.AddOption(pathOption);
initCommand.AddOption(profilesOption);
initCommand.AddOption(assemblyPathOption);
initCommand.AddOption(solutionOption);
initCommand.AddOption(forceOption);
CommandValidators.AddInitValidators(initCommand, pathOption, profilesOption);
initCommand.SetHandler(InitAsync, pathOption, profilesOption, assemblyPathOption, solutionOption, forceOption);

var earlyBoundConfigOption = new Option<string?>(
    aliases: ["--config"],
    description: "Path to earlyboundgenerator.xml or .json config (default depends on --json-config).");

var earlyBoundJsonConfigOption = new Option<bool>(
    aliases: ["--json-config"],
    description: "Use JSON config from earlybound.json in --path (or path from --config).");

var earlyBoundOutputOption = new Option<DirectoryInfo?>(
    aliases: ["--output", "-o"],
    description: "Output directory for generated early bound files (default: EarlyBound under --path).");

var earlyBoundNamespaceOption = new Option<string?>(
    aliases: ["--namespace", "-n"],
    description: "C# namespace for generated types.");

var earlyBoundServiceContextOption = new Option<string?>(
    aliases: ["--service-context"],
    description: "Name of the generated OrganizationServiceContext class.");

var earlyBoundEntitiesOption = new Option<string?>(
    aliases: ["--entities", "-e"],
    description: "Pipe-separated entity logical names to include (e.g. account|contact).");

var earlyBoundSkipMessagesOption = new Option<bool>(
    aliases: ["--skip-messages"],
    description: "Skip generating SDK message / action types.");

var earlyBoundGlobalOptionSetsOption = new Option<bool>(
    aliases: ["--global-option-sets"],
    description: "Generate global option sets.");

var earlyBoundInitConfigOption = new Option<bool>(
    aliases: ["--init-config"],
    description: "Create a default earlyboundgenerator.xml or earlybound.json and exit.");

var earlyBoundForceOption = new Option<bool>(
    aliases: ["--force"],
    description: "Overwrite existing config file when using --init-config.");

var earlyBoundOverwriteOption = new Option<bool>(
    aliases: ["--overwrite"],
    description: "Overwrite existing generated .cs files and config file (--init-config).");

var earlyBoundCommand = new Command(
    "earlybound",
    "Generate early-bound Dataverse entities, option sets, and actions using DLaB Early Bound Generator V2.");
earlyBoundCommand.AddOption(pathOption);
earlyBoundCommand.AddOption(connectionOption);
earlyBoundCommand.AddOption(earlyBoundConfigOption);
earlyBoundCommand.AddOption(earlyBoundJsonConfigOption);
earlyBoundCommand.AddOption(earlyBoundOutputOption);
earlyBoundCommand.AddOption(earlyBoundNamespaceOption);
earlyBoundCommand.AddOption(earlyBoundServiceContextOption);
earlyBoundCommand.AddOption(earlyBoundEntitiesOption);
earlyBoundCommand.AddOption(earlyBoundSkipMessagesOption);
earlyBoundCommand.AddOption(earlyBoundGlobalOptionSetsOption);
earlyBoundCommand.AddOption(earlyBoundInitConfigOption);
earlyBoundCommand.AddOption(earlyBoundForceOption);
earlyBoundCommand.AddOption(earlyBoundOverwriteOption);
CommandValidators.AddEarlyBoundValidators(
    earlyBoundCommand,
    pathOption,
    connectionOption,
    earlyBoundInitConfigOption);
earlyBoundCommand.SetHandler(context => EarlyBoundAsync(
    context.ParseResult.GetValueForOption(pathOption),
    context.ParseResult.GetValueForOption(connectionOption),
    context.ParseResult.GetValueForOption(earlyBoundConfigOption),
    context.ParseResult.GetValueForOption(earlyBoundJsonConfigOption),
    context.ParseResult.GetValueForOption(earlyBoundOutputOption),
    context.ParseResult.GetValueForOption(earlyBoundNamespaceOption),
    context.ParseResult.GetValueForOption(earlyBoundServiceContextOption),
    context.ParseResult.GetValueForOption(earlyBoundEntitiesOption),
    context.ParseResult.GetValueForOption(earlyBoundSkipMessagesOption),
    context.ParseResult.GetValueForOption(earlyBoundGlobalOptionSetsOption),
    context.ParseResult.GetValueForOption(earlyBoundInitConfigOption),
    context.ParseResult.GetValueForOption(earlyBoundForceOption),
    context.ParseResult.GetValueForOption(earlyBoundOverwriteOption)));

root.AddCommand(deployCommand);
root.AddCommand(syncCommand);
root.AddCommand(whoamiCommand);
root.AddCommand(initCommand);
root.AddCommand(earlyBoundCommand);

var commandLineBuilder = new CommandLineBuilder(root);
commandLineBuilder.UseDefaults();
commandLineBuilder.UseExceptionHandler((exception, context) => CliErrorReporter.ReportException(exception, context));

return await commandLineBuilder.Build().InvokeAsync(args);

static DirectoryInfo ResolvePath(DirectoryInfo? path)
    => PathValidation.Resolve(path);

static IOrganizationService Connect(string? connection)
{
    // The factory now handles:
    // - explicit connection string
    // - DATAVERSE_ACCESS_TOKEN (for Workload Identity Federation)
    // - AZURE_* / DATAVERSE_* secrets or certificates (from Azure DevOps Service Connections)
    return DataverseConnectionFactory.Connect(connection);
}

static Task DeployAsync(
    DirectoryInfo? path,
    string? profile,
    string? connection,
    bool excludeSteps,
    bool workflow)
{
    var trace = new ConsoleTrace();
    var service = Connect(connection);
    var deployService = new PluginDeployService(service, trace);

    trace.WriteLine("Deploying plugins for profile '{0}'", profile ?? "default");
    deployService.Deploy(ResolvePath(path).FullName, profile, excludeSteps, workflow);
    trace.WriteLine("Deployment completed successfully.");
    return Task.CompletedTask;
}

static Task SyncAsync(DirectoryInfo? path, string? connection, string? classRegex)
{
    var trace = new ConsoleTrace();
    var service = Connect(connection);
    var syncService = new MetadataSyncService(service, trace);

    trace.WriteLine("Syncing plugin metadata into source code.");
    syncService.SyncSourceCode(ResolvePath(path).FullName, classRegex);
    return Task.CompletedTask;
}

static Task InitAsync(
    DirectoryInfo? path,
    string profiles,
    string assemblyPath,
    string? solution,
    bool force)
{
    var trace = new ConsoleTrace();
    var workingDirectory = ResolvePath(path).FullName;
    var profileList = profiles
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(p => !string.IsNullOrWhiteSpace(p))
        .ToArray();

    var scaffold = new ConfigScaffoldService(trace);
    scaffold.Generate(workingDirectory, profileList, assemblyPath, solution, force);
    return Task.CompletedTask;
}

static Task WhoAmIAsync(string? connection)
{
    var service = Connect(connection);
    var whoAmI = DataverseOrganizationRequests.WhoAmI(service);
    Console.WriteLine($"OrganizationId: {whoAmI.OrganizationId}");
    Console.WriteLine($"BusinessUnitId: {whoAmI.BusinessUnitId}");
    Console.WriteLine($"UserId: {whoAmI.UserId}");
    return Task.CompletedTask;
}

static Task EarlyBoundAsync(
    DirectoryInfo? path,
    string? connection,
    string? config,
    bool jsonConfig,
    DirectoryInfo? output,
    string? @namespace,
    string? serviceContext,
    string? entities,
    bool skipMessages,
    bool globalOptionSets,
    bool initConfig,
    bool force,
    bool overwrite)
{
    var trace = new ConsoleTrace();
    var workingDirectory = ResolvePath(path).FullName;
    var request = new EarlyBoundGenerationRequest
    {
        WorkingDirectory = workingDirectory,
        ConfigFilePath = config,
        UseJsonConfig = jsonConfig,
        OutputDirectory = output?.FullName,
        Namespace = @namespace,
        ServiceContextName = serviceContext,
        EntitiesWhitelist = entities,
        GenerateMessages = skipMessages ? false : null,
        GenerateGlobalOptionSets = globalOptionSets ? true : null,
        OverwriteExistingFiles = overwrite ? true : null,
        InitConfigOnly = initConfig,
        ForceInitConfig = force || overwrite
    };

    if (initConfig)
    {
        var scaffoldService = new EarlyBoundGeneratorService(null!, trace);
        scaffoldService.Generate(request);
        return Task.CompletedTask;
    }

    var service = Connect(connection);
    var generatorService = new EarlyBoundGeneratorService(service, trace);
    trace.WriteLine("Starting early bound generation.");
    generatorService.Generate(request);
    return Task.CompletedTask;
}