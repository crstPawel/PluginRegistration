using System.Reflection;
using Microsoft.Xrm.Sdk;
using PluginRegistration.Core.Connection;
using PluginRegistration.Attributes;
using PluginRegistration.Core.Config;

namespace PluginRegistration.Core.Registration;

public sealed class PluginRegistrationService
{
    private static readonly int[] SupportedPluginStages = [10, 20, 40, 50];

    private readonly IOrganizationService _service;
    private readonly DataverseQueries _queries;
    private readonly ITrace _trace;
    private readonly EnvironmentConfigurationResolver _environmentResolver;

    public PluginRegistrationService(
        IOrganizationService service,
        ITrace trace,
        PluginRegistrationConfig config)
    {
        _service = service;
        _queries = new DataverseQueries(service);
        _trace = trace;
        _environmentResolver = new EnvironmentConfigurationResolver(config.StepOverrides, config.CustomApis);
    }

    public string? SolutionUniqueName { get; set; }

    public void RegisterPlugins(string assemblyPath, bool excludePluginSteps = false)
    {
        var file = new FileInfo(assemblyPath);
        if (file.Name.StartsWith("System.", StringComparison.Ordinal) || ReflectionHelper.ShouldIgnoreAssembly(file.Name))
        {
            return;
        }

        using var context = ReflectionHelper.CreateLoadContext(file.DirectoryName!);
        var assembly = ReflectionHelper.LoadAssembly(context, file.FullName);
        if (assembly is null)
        {
            return;
        }

        var pluginTypes = ReflectionHelper.GetPluginTypes(assembly).ToList();
        if (pluginTypes.Count == 0)
        {
            return;
        }

        _trace.WriteLine("Checking assembly '{0}' - found {1} plugin(s)", file.Name, pluginTypes.Count);

        var pluginAssemblyId = RegisterAssembly(file, assembly, pluginTypes);
        if (pluginAssemblyId is null || excludePluginSteps)
        {
            return;
        }

        RegisterPluginSteps(pluginTypes, pluginAssemblyId.Value);
    }

    public void RegisterPluginPackage(string packagePath, bool excludePluginSteps = false)
    {
        FileInfo file = new FileInfo(packagePath);
        if (!file.Exists)
        {
            throw new PluginRegistrationException($"Plugin package not found: {packagePath}");
        }

        string packageId = NuGetPackageReader.GetPackageId(file.FullName);
        _trace.WriteLine("Deploying plugin package '{0}' ({1})", packageId, file.Name);

        Guid packageEntityId = UpsertPluginPackage(packageId, file.FullName);
        string tempDirectory = NuGetPackageReader.ExtractToTempDirectory(file.FullName);

        try
        {
            foreach (string assemblyPath in NuGetPackageReader.GetPluginAssemblyPaths(tempDirectory))
            {
                using MetadataLoadContext context = ReflectionHelper.CreateLoadContext(Path.GetDirectoryName(assemblyPath)!);
                Assembly? assembly = ReflectionHelper.LoadAssembly(context, assemblyPath);
                if (assembly is null)
                {
                    continue;
                }

                List<Type> pluginTypes = ReflectionHelper.GetPluginTypes(assembly).ToList();
                if (pluginTypes.Count == 0)
                {
                    continue;
                }

                string assemblyName = assembly.GetName().Name!;
                _trace.WriteLine("Checking package assembly '{0}' - found {1} plugin(s)", assemblyName, pluginTypes.Count);

                Guid? pluginAssemblyId = ResolvePackageAssemblyId(packageEntityId, assemblyName);
                if (pluginAssemblyId is null)
                {
                    _trace.WriteLine(
                        "Warning: Assembly '{0}' was not found in package '{1}' after upload. Skipping step registration.",
                        assemblyName,
                        packageId);
                    continue;
                }

                if (excludePluginSteps)
                {
                    continue;
                }

                RemoveOrphanedPluginTypes(pluginTypes, pluginAssemblyId.Value, false);
                RegisterPluginSteps(pluginTypes, pluginAssemblyId.Value);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(tempDirectory, true);
            }
            catch (IOException)
            {
                // Best-effort cleanup of the temp extraction directory.
            }
        }
    }

    private Guid? RegisterAssembly(
        FileInfo assemblyFile,
        Assembly assembly,
        IEnumerable<Type> pluginTypes,
        bool isWorkflowActivity = false)
    {
        var hasPluginSteps = pluginTypes.Any(type => ReflectionHelper.GetRegistrationAttributes(type).Any());
        var hasCustomApis = pluginTypes.Any(type => ReflectionHelper.GetCustomApiRegistrationAttributes(type).Any());

        if (!hasPluginSteps && !hasCustomApis)
        {
            return null;
        }

        var firstAttribute = hasPluginSteps
            ? pluginTypes
                .SelectMany(ReflectionHelper.GetRegistrationAttributes)
                .Select(AttributeParser.Parse)
                .FirstOrDefault()
            : null;

        var assemblyName = assembly.GetName();
        var existing = _queries.GetPluginAssemblyByName(assemblyName.Name!);
        if (existing?.GetAttributeValue<EntityReference>("packageid") is not null)
        {
            throw new PluginRegistrationException(
                $"Assembly '{assemblyName.Name}' is managed by a plugin package. " +
                $"Deploy the .nupkg file instead of updating the assembly directly.");
        }

        var content = Convert.ToBase64String(File.ReadAllBytes(assemblyFile.FullName));

        var record = existing ?? new Entity("pluginassembly");
        record["content"] = content;
        record["name"] = assemblyName.Name;
        record["culture"] = assemblyName.CultureName ?? "neutral";
        record["version"] = assemblyName.Version?.ToString() ?? "1.0.0.0";
        record["publickeytoken"] = BitConverter.ToString(assemblyName.GetPublicKeyToken() ?? []).Replace("-", string.Empty).ToLowerInvariant();
        record["sourcetype"] = new OptionSetValue(0);
        record["isolationmode"] = new OptionSetValue(
            firstAttribute?.IsolationMode == IsolationModeEnum.Sandbox ? 2 : 1);

        Guid assemblyId;
        if (existing is null)
        {
            _trace.WriteLine("Registering plugin assembly '{0}'", assemblyName.Name);
            assemblyId = _service.Create(record);
        }
        else
        {
            _trace.WriteLine("Updating plugin assembly '{0}'", assemblyName.Name);
            record.Id = existing.Id;
            _service.Update(record);
            assemblyId = existing.Id;
            RemoveOrphanedPluginTypes(pluginTypes, assemblyId, isWorkflowActivity);
        }

        if (!string.IsNullOrWhiteSpace(SolutionUniqueName))
        {
            AddComponentToSolution(SolutionUniqueName, 91, assemblyId, addRequiredComponents: true);
        }

        return assemblyId;
    }

    private Guid UpsertPluginPackage(string packageId, string packagePath)
    {
        string content = Convert.ToBase64String(File.ReadAllBytes(packagePath));
        Entity? existing = _queries.GetPluginPackageByName(packageId);

        if (existing is null)
        {
            _trace.WriteLine("Registering plugin package '{0}'", packageId);
            Entity record = new Entity("pluginpackage")
            {
                ["name"] = packageId,
                ["content"] = content
            };

            return DataverseOrganizationRequests.CreateWithSolution(_service, record, SolutionUniqueName);
        }

        _trace.WriteLine("Updating plugin package '{0}'", packageId);
        Entity update = new Entity("pluginpackage", existing.Id)
        {
            ["content"] = content
        };
        DataverseOrganizationRequests.UpdateWithSolution(_service, update, SolutionUniqueName);
        return existing.Id;
    }

    private Guid? ResolvePackageAssemblyId(Guid packageId, string assemblyName)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            Entity? assembly = _queries.GetPluginAssembliesForPackage(packageId)
                .FirstOrDefault(record => String.Equals(
                    record.GetAttributeValue<string>("name"),
                    assemblyName,
                    StringComparison.Ordinal));

            if (assembly is not null)
            {
                return assembly.Id;
            }

            if (attempt < 4)
            {
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
        }

        return null;
    }

    private void RemoveOrphanedPluginTypes(IEnumerable<Type> pluginTypes, Guid assemblyId, bool isWorkflowActivity)
    {
        var typeNames = pluginTypes.Select(t => t.FullName).ToHashSet(StringComparer.Ordinal);
        var existingTypes = _queries.GetPluginTypes(assemblyId, isWorkflowActivity);

        foreach (var existingType in existingTypes)
        {
            var typeName = existingType.GetAttributeValue<string>("typename");
            if (typeNames.Contains(typeName))
            {
                continue;
            }

            _trace.WriteLine("Removing orphaned plugin type '{0}'", typeName);
            foreach (var step in _queries.GetPluginSteps(existingType.Id))
            {
                _trace.WriteLine("Deleting step '{0}'", step.GetAttributeValue<string>("name"));
                _service.Delete("sdkmessageprocessingstep", step.Id);
            }

            _service.Delete("plugintype", existingType.Id);
        }
    }

    private void RegisterPluginSteps(IEnumerable<Type> pluginTypes, Guid assemblyId)
    {
        var existingTypes = _queries.GetPluginTypes(assemblyId).ToDictionary(
            t => t.GetAttributeValue<string>("typename")!,
            t => t,
            StringComparer.Ordinal);

        foreach (var pluginType in pluginTypes)
        {
            var attributeData = ReflectionHelper.GetRegistrationAttributes(pluginType).ToList();
            var customApiData = ReflectionHelper.GetCustomApiRegistrationAttributes(pluginType).ToList();

            if (attributeData.Count == 0 && customApiData.Count == 0)
            {
                continue;
            }

            var pluginTypeId = UpsertPluginType(pluginType, assemblyId, existingTypes);
            var existingSteps = _queries.GetPluginSteps(pluginTypeId);

            foreach (var data in attributeData)
            {
                var attribute = AttributeParser.Parse(data);
                if (!AttributeParser.IsPluginStepRegistration(attribute))
                {
                    continue;
                }

                var stepAttribute = PluginStepNameResolver.ApplyStepName(
                    pluginType,
                    _environmentResolver.ApplyStepOverrides(attribute));
                RegisterStep(pluginType, pluginTypeId, existingSteps, stepAttribute);
            }

            foreach (var data in customApiData)
            {
                var attribute = CustomApiAttributeParser.Parse(data);
                RegisterCustomApi(pluginType, pluginTypeId, attribute);
            }

            foreach (var step in existingSteps)
            {
                var stage = step.GetAttributeValue<OptionSetValue>("stage")?.Value ?? 0;
                if (SupportedPluginStages.Contains(stage))
                {
                    _trace.WriteLine("Deleting obsolete step '{0}'", step.GetAttributeValue<string>("name"));
                    _service.Delete("sdkmessageprocessingstep", step.Id);
                }
            }
        }
    }

    private Guid UpsertPluginType(Type pluginType, Guid assemblyId, Dictionary<string, Entity> existingTypes)
    {
        if (existingTypes.TryGetValue(pluginType.FullName!, out var existing))
        {
            var update = new Entity("plugintype", existing.Id)
            {
                ["name"] = pluginType.FullName,
                ["typename"] = pluginType.FullName,
                ["friendlyname"] = pluginType.FullName,
                ["pluginassemblyid"] = new EntityReference("pluginassembly", assemblyId)
            };

            _trace.WriteLine("Updating plugin type '{0}'", pluginType.FullName);
            _service.Update(update);
            return existing.Id;
        }

        var create = new Entity("plugintype")
        {
            ["name"] = pluginType.FullName,
            ["typename"] = pluginType.FullName,
            ["friendlyname"] = pluginType.FullName,
            ["pluginassemblyid"] = new EntityReference("pluginassembly", assemblyId)
        };

        _trace.WriteLine("Registering plugin type '{0}'", pluginType.FullName);
        return _service.Create(create);
    }

    private void RegisterCustomApi(Type pluginType, Guid pluginTypeId, CustomApiRegistration attribute)
    {
        var profileOverride = _environmentResolver.GetCustomApiOverride(attribute.UniqueName);
        var model = CustomApiAttributeReader.Read(pluginType, attribute, profileOverride);
        var customApiService = new CustomApiRegistrationService(_service, _trace)
        {
            SolutionUniqueName = SolutionUniqueName
        };

        customApiService.RegisterCustomApi(model, pluginTypeId);
    }

    private void RegisterStep(
        Type pluginType,
        Guid pluginTypeId,
        List<Entity> existingSteps,
        PluginRegistrationAttribute pluginStep)
    {
        Entity? step = null;
        if (!string.IsNullOrWhiteSpace(pluginStep.Id) && Guid.TryParse(pluginStep.Id, out var stepId))
        {
            step = existingSteps.FirstOrDefault(s => s.Id == stepId);
        }

        if (step is null)
        {
            step = existingSteps.FirstOrDefault(s =>
                string.Equals(s.GetAttributeValue<string>("name"), pluginStep.Name, StringComparison.Ordinal)
                && string.Equals(
                    _queries.GetMessageName(s.GetAttributeValue<EntityReference>("sdkmessageid").Id),
                    pluginStep.Message,
                    StringComparison.Ordinal));
        }

        var record = step is null ? new Entity("sdkmessageprocessingstep") : new Entity("sdkmessageprocessingstep", step.Id);

        Guid messageId;
        Guid? messageFilterId = null;
        if (string.Equals(pluginStep.EntityLogicalName, "none", StringComparison.OrdinalIgnoreCase))
        {
            var id = _queries.GetMessageId(pluginStep.Message!);
            if (id is null)
            {
                _trace.WriteLine("Warning: Cannot register step '{0}' - message not found", pluginStep.Message);
                return;
            }

            messageId = id.Value;
        }
        else
        {
            var filter = _queries.GetMessageFilter(pluginStep.EntityLogicalName!, pluginStep.Message!);
            if (filter is null)
            {
                _trace.WriteLine("Warning: Cannot register step '{0}' on entity '{1}'", pluginStep.Message, pluginStep.EntityLogicalName);
                return;
            }

            messageId = filter.Value.MessageId;
            messageFilterId = filter.Value.FilterId;
        }

        record["name"] = pluginStep.Name;
        record["configuration"] = pluginStep.UnSecureConfiguration;
        record["mode"] = new OptionSetValue(pluginStep.ExecutionMode == ExecutionModeEnum.Asynchronous ? 1 : 0);
        record["asyncautodelete"] = pluginStep.ExecutionMode == ExecutionModeEnum.Asynchronous && pluginStep.DeleteAsyncOperation;
        record["rank"] = pluginStep.ExecutionOrder;
        record["stage"] = new OptionSetValue((int)pluginStep.Stage!.Value);
        record["supporteddeployment"] = new OptionSetValue(GetSupportedDeployment(pluginStep));
        record["plugintypeid"] = new EntityReference("plugintype", pluginTypeId);
        record["sdkmessageid"] = new EntityReference("sdkmessage", messageId);
        record["filteringattributes"] = NormalizeCommaSeparated(pluginStep.FilteringAttributes ?? []);

        if (messageFilterId.HasValue)
        {
            record["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", messageFilterId.Value);
        }

        Guid registeredStepId;
        if (step is null)
        {
            if (!string.IsNullOrWhiteSpace(pluginStep.Id) && Guid.TryParse(pluginStep.Id, out var requestedId))
            {
                record.Id = requestedId;
            }

            _trace.WriteLine("Registering step '{0}'", pluginStep.Name);
            registeredStepId = _service.Create(record);
        }
        else
        {
            _trace.WriteLine("Updating step '{0}'", pluginStep.Name);
            _service.Update(record);
            registeredStepId = step.Id;
            existingSteps.Remove(step);
        }

        RegisterSecureConfiguration(registeredStepId, pluginStep.SecureConfiguration);
        RegisterImages(registeredStepId, pluginType, pluginStep);

        if (!string.IsNullOrWhiteSpace(SolutionUniqueName))
        {
            AddComponentToSolution(SolutionUniqueName, 92, registeredStepId);
        }
    }

    private void RegisterSecureConfiguration(Guid stepId, string? secureConfiguration)
    {
        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("sdkmessageprocessingstepsecureconfig")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("sdkmessageprocessingstepsecureconfigid", "secureconfig"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
            {
                Conditions =
                {
                    new Microsoft.Xrm.Sdk.Query.ConditionExpression("sdkmessageprocessingstepid", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, stepId)
                }
            },
            TopCount = 1
        };

        var existing = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(secureConfiguration))
        {
            if (existing is not null)
            {
                _service.Delete("sdkmessageprocessingstepsecureconfig", existing.Id);
            }

            return;
        }

        var record = existing ?? new Entity("sdkmessageprocessingstepsecureconfig");
        record["secureconfig"] = secureConfiguration;
        record["sdkmessageprocessingstepid"] = new EntityReference("sdkmessageprocessingstep", stepId);

        if (existing is null)
        {
            _service.Create(record);
        }
        else
        {
            record.Id = existing.Id;
            _service.Update(record);
        }
    }

    private void RegisterImages(Guid stepId, Type pluginType, PluginRegistrationAttribute pluginStep)
    {
        var existingImages = _queries.GetPluginStepImages(stepId);

        foreach (var image in PluginStepImageReader.GetImages(pluginType, pluginStep))
        {
            RegisterImage(stepId, pluginStep, existingImages, image.Name, image.ImageType, image.Attributes);
        }

        foreach (var image in existingImages)
        {
            _trace.WriteLine("Deleting obsolete image '{0}'", image.GetAttributeValue<string>("name"));
            _service.Delete("sdkmessageprocessingstepimage", image.Id);
        }
    }

    private void RegisterImage(
        Guid stepId,
        PluginRegistrationAttribute pluginStep,
        List<Entity> existingImages,
        string? imageName,
        ImageTypeEnum imageType,
        string? attributes)
    {
        if (string.IsNullOrWhiteSpace(imageName))
        {
            return;
        }

        var image = existingImages.FirstOrDefault(i =>
                String.Equals(i.GetAttributeValue<string>("entityalias"), imageName, StringComparison.Ordinal)
                && i.GetAttributeValue<OptionSetValue>("imagetype")?.Value == (int)imageType)
            ?? new Entity("sdkmessageprocessingstepimage");

        image["name"] = imageName;
        image["entityalias"] = imageName;
        image["imagetype"] = new OptionSetValue((int)imageType);
        image["attributes"] = NormalizeCommaSeparated(attributes);
        image["sdkmessageprocessingstepid"] = new EntityReference("sdkmessageprocessingstep", stepId);
        image["messagepropertyname"] = GetImageMessagePropertyName(pluginStep.Message!);

        if (image.Id == Guid.Empty)
        {
            _trace.WriteLine("Registering image '{0}'", imageName);
            _service.Create(image);
        }
        else
        {
            _trace.WriteLine("Updating image '{0}'", imageName);
            _service.Update(image);
            existingImages.Remove(image);
        }
    }

    private static int GetSupportedDeployment(PluginRegistrationAttribute pluginStep)
        => pluginStep.Server ? 0 : 0;

    private static string? NormalizeCommaSeparated(string[] input)
    {
        if (input.Length == 0)
        {
            return null;
        }

        return string.Join(",", input).Replace(" ", string.Empty);
    }

    private static string? NormalizeCommaSeparated(string? input)
        => string.IsNullOrWhiteSpace(input) ? input : input.Replace(" ", string.Empty);

    private static string GetImageMessagePropertyName(string message) => message switch
    {
        "Create" => "Id",
        "SetState" or "SetStateDynamicEntity" => "EntityMoniker",
        "Send" or "DeliverIncoming" or "DeliverPromote" => "EmailId",
        _ => "Target"
    };

    private void AddComponentToSolution(string solutionName, int componentType, Guid componentId, bool addRequiredComponents = false)
    {
        _trace.WriteLine("Adding component to solution '{0}'", solutionName);
        DataverseOrganizationRequests.AddSolutionComponent(
            _service,
            solutionName,
            componentType,
            componentId,
            addRequiredComponents);
    }
}
