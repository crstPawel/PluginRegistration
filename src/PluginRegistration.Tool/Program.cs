using System;
using Microsoft.Xrm.Sdk;
using PluginRegistration.Core;
using PluginRegistration.Core.Connection;
using PluginRegistration.Core.Deploy;
using PluginRegistration.Core.EarlyBound;
using PluginRegistration.Core.Sync;
using PluginRegistration.Tool.Cli;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.IO;
using System.Threading.Tasks;

namespace PluginRegistration.Tool
{
    internal class Program
    {
        public static async Task<int> Main(string[] args)
        {
            RootCommand root = new RootCommand("Dataverse plugin registration tool for Azure DevOps pipelines.");

            var pathOption = new Option<DirectoryInfo>(
                aliases: ["--path", "-p"],
                description: "Working directory for plugin packages / source (default: current directory).")
            {
                IsRequired = false
            };

            var connectionOption = new Option<string?>(
                aliases: ["--connection", "-c"],
                description: "Dataverse connection string. If omitted, DATAVERSE_* environment variables are used.");

            var packagePathOption = new Option<string>(
                aliases: ["--package-path"],
                getDefaultValue: () => "bin/Release",
                description: "Folder or pattern for plugin NuGet packages (*.nupkg), relative to --path.");

            var solutionOption = new Option<string?>(
                aliases: ["--solution", "-s"],
                description: "Dataverse solution unique name (components + publisher prefix for package/Custom API names).");

            var excludeStepsOption = new Option<bool>(
                aliases: ["--exclude-steps"],
                description: "Upload plugin packages only, skip plugin step and Custom API registration.");

            var classRegexOption = new Option<string?>(
                aliases: ["--class-regex"],
                description: "Custom regex for detecting plugin classes during sync.");

            var deployCommand = new Command("deploy", "Deploy plugin NuGet packages and register steps/Custom APIs.");
            deployCommand.AddOption(pathOption);
            deployCommand.AddOption(packagePathOption);
            deployCommand.AddOption(solutionOption);
            deployCommand.AddOption(connectionOption);
            deployCommand.AddOption(excludeStepsOption);
            CommandValidators.AddDeployValidators(deployCommand, pathOption, connectionOption);
            deployCommand.SetHandler(
                DeployAsync,
                pathOption,
                packagePathOption,
                solutionOption,
                connectionOption,
                excludeStepsOption);

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

            var earlyBoundConfigOption = new Option<string?>(
                aliases: ["--config"],
                description: "Path to DLaB EBG V2 earlyboundgenerator.xml (default: earlyboundgenerator.xml under --path).");

            var earlyBoundOutputOption = new Option<DirectoryInfo?>(
                aliases: ["--output", "-o"],
                description: "Output directory for generated early bound files (overrides RootPath in XML; default: EarlyBound under --path).");

            var earlyBoundNamespaceOption = new Option<string?>(
                aliases: ["--namespace", "-n"],
                description: "C# namespace for generated types (overrides Namespace in XML).");

            var earlyBoundServiceContextOption = new Option<string?>(
                aliases: ["--service-context"],
                description: "Name of the generated OrganizationServiceContext class (overrides ServiceContextName in XML).");

            var earlyBoundEntitiesOption = new Option<string?>(
                aliases: ["--entities", "-e"],
                description: "Pipe-separated entity logical names to include (overrides EntitiesWhitelist in XML, e.g. account|contact).");

            var earlyBoundSkipMessagesOption = new Option<bool>(
                aliases: ["--skip-messages"],
                description: "Skip generating SDK message / action types (overrides GenerateMessages in XML).");

            var earlyBoundGlobalOptionSetsOption = new Option<bool>(
                aliases: ["--global-option-sets"],
                description: "Generate global option sets (overrides GenerateGlobalOptionSets in XML).");

            var earlyBoundInitConfigOption = new Option<bool>(
                aliases: ["--init-config"],
                description: "Create a default DLaB EBG V2 earlyboundgenerator.xml and exit.");

            var earlyBoundForceOption = new Option<bool>(
                aliases: ["--force"],
                description: "Overwrite existing earlyboundgenerator.xml when using --init-config.");

            var earlyBoundOverwriteOption = new Option<bool>(
                aliases: ["--overwrite"],
                description: "Overwrite existing generated .cs files; with --init-config also replaces the XML config.");

            var earlyBoundCommand = new Command(
                "earlybound",
                "Generate early-bound Dataverse entities, option sets, and actions using DLaB Early Bound Generator V2 XML config.");
            earlyBoundCommand.AddOption(pathOption);
            earlyBoundCommand.AddOption(connectionOption);
            earlyBoundCommand.AddOption(earlyBoundConfigOption);
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
            root.AddCommand(earlyBoundCommand);

            var commandLineBuilder = new CommandLineBuilder(root);
            commandLineBuilder.UseDefaults();
            commandLineBuilder.UseExceptionHandler((exception, context) => CliErrorReporter.ReportException(exception, context));

            return await commandLineBuilder.Build().InvokeAsync(args);

            static DirectoryInfo ResolvePath(DirectoryInfo? path)
                => PathValidation.Resolve(path);

            static IOrganizationService Connect(string? connection)
            {
                return DataverseConnectionFactory.Connect(connection);
            }

            static Task DeployAsync(
                DirectoryInfo? path,
                string packagePath,
                string? solution,
                string? connection,
                bool excludeSteps)
            {
                var trace = new ConsoleTrace();
                var service = Connect(connection);
                var deployService = new PluginDeployService(service, trace);
                var workingDirectory = ResolvePath(path).FullName;

                trace.WriteLine("Deploying plugins.");
                deployService.Deploy(workingDirectory, packagePath, solution, excludeSteps);
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
        }
    }
}
