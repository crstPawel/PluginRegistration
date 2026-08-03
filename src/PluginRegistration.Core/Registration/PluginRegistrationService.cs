using System.Reflection;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using PluginRegistration.Core.Connection;
using PluginRegistration.Attributes;
using PluginRegistration.Core.Model.Entities;

namespace PluginRegistration.Core.Registration;

public sealed class PluginRegistrationService
{
    private static readonly int[] SupportedPluginStages = [10, 20, 40, 50];

    private readonly IOrganizationService _service;
    private readonly DataverseQueries _queries;
    private readonly ITrace _trace;
    private readonly SolutionEnsureService _solutionEnsure;
    private readonly Dictionary<string, string> _publisherPrefixBySolution = new(StringComparer.OrdinalIgnoreCase);

    public PluginRegistrationService(IOrganizationService service, ITrace trace)
    {
        _service = service;
        _queries = new DataverseQueries(service);
        _trace = trace;
        _solutionEnsure = new SolutionEnsureService(service, trace);
    }

    public string? SolutionUniqueName { get; set; }

    public void RegisterPluginPackage(string packagePath, bool excludePluginSteps = false)
    {
        FileInfo file = new FileInfo(packagePath);
        if (!file.Exists)
        {
            throw new PluginRegistrationException($"Plugin package not found: {packagePath}");
        }

        (string packageId, string packageVersion) = NuGetPackageReader.GetPackageMetadata(file.FullName);
        string registrationName = ResolvePluginPackageRegistrationName(packageId);
        if (!string.Equals(registrationName, packageId, StringComparison.Ordinal))
        {
            _trace.WriteLine(
                "Deploying plugin package '{0}' v{1} as '{2}' ({3})",
                packageId,
                packageVersion,
                registrationName,
                file.Name);
        }
        else
        {
            _trace.WriteLine("Deploying plugin package '{0}' v{1} ({2})", packageId, packageVersion, file.Name);
        }

        string tempDirectory = NuGetPackageReader.ExtractToTempDirectory(file.FullName);

        try
        {
            // Discover types from the new package before upload. Dataverse removes plugintypes that
            // disappear from package content during update; that fails if steps/Custom APIs still
            // reference those types — so clear those dependencies first.
            Dictionary<string, HashSet<string>> expectedTypesByAssembly =
                DiscoverExpectedPluginTypesByAssembly(tempDirectory);

            Entity? existingPackage = FindExistingPluginPackage(registrationName, packageId);
            if (existingPackage is not null)
            {
                RemoveDependenciesForTypesLeavingPackage(existingPackage.Id, expectedTypesByAssembly);
            }

            Guid packageEntityId = UpsertPluginPackage(
                packageId,
                registrationName,
                packageVersion,
                file.FullName,
                existingPackage);

            foreach (string assemblyPath in NuGetPackageReader.GetPluginAssemblyPaths(tempDirectory))
            {
                using MetadataLoadContext context = ReflectionHelper.CreateLoadContext(Path.GetDirectoryName(assemblyPath)!);
                Assembly? assembly = ReflectionHelper.LoadAssembly(context, assemblyPath);
                if (assembly is null)
                {
                    continue;
                }

                List<Type> pluginTypes = ReflectionHelper.GetPluginTypes(assembly).ToList();
                string assemblyName = assembly.GetName().Name!;
                _trace.WriteLine("Checking package assembly '{0}' - found {1} plugin(s)", assemblyName, pluginTypes.Count);

                Guid? pluginAssemblyId = ResolvePackageAssemblyId(packageEntityId, assemblyName);
                if (pluginAssemblyId is null)
                {
                    if (pluginTypes.Count > 0)
                    {
                        _trace.WriteLine(
                            "Warning: Assembly '{0}' was not found in package '{1}' after upload. Skipping step registration.",
                            assemblyName,
                            packageId);
                    }

                    continue;
                }

                // Package-managed assemblies: Dataverse creates plugintype rows from package content.
                // Creating plugintype client-side produces rows that fail step validation with
                // "PluginType not found in PluginAssembly ... total of [0] plugin/workflow activity types".
                var expectedTypeNames = pluginTypes
                    .Select(type => type.FullName!)
                    .ToList();
                WaitForPackagePluginTypes(
                    pluginAssemblyId.Value,
                    assemblyName,
                    expectedTypeNames);

                // Safety net: remove any plugintypes still present but no longer in the package.
                RemoveOrphanedPluginTypes(pluginTypes, pluginAssemblyId.Value, false);

                if (excludePluginSteps || pluginTypes.Count == 0)
                {
                    continue;
                }

                // Re-query after orphan cleanup so registration uses current server state.
                Dictionary<string, Entity> serverTypes = _queries
                    .GetPluginTypes(pluginAssemblyId.Value, isWorkflowActivity: false)
                    .Where(record => !string.IsNullOrWhiteSpace(record.GetAttributeValue<string>("typename")))
                    .ToDictionary(
                        record => record.GetAttributeValue<string>("typename")!,
                        record => record,
                        StringComparer.OrdinalIgnoreCase);

                RegisterPluginStepsFromPackage(pluginTypes, serverTypes);
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

    /// <summary>
    /// Maps assembly simple name → full plugin type names present in the package.
    /// </summary>
    private static Dictionary<string, HashSet<string>> DiscoverExpectedPluginTypesByAssembly(string tempDirectory)
    {
        Dictionary<string, HashSet<string>> result = new(StringComparer.OrdinalIgnoreCase);

        foreach (string assemblyPath in NuGetPackageReader.GetPluginAssemblyPaths(tempDirectory))
        {
            using MetadataLoadContext context = ReflectionHelper.CreateLoadContext(Path.GetDirectoryName(assemblyPath)!);
            Assembly? assembly = ReflectionHelper.LoadAssembly(context, assemblyPath);
            if (assembly is null)
            {
                continue;
            }

            string assemblyName = assembly.GetName().Name!;
            HashSet<string> typeNames = ReflectionHelper.GetPluginTypes(assembly)
                .Select(type => type.FullName!)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            result[assemblyName] = typeNames;
        }

        return result;
    }

    private Entity? FindExistingPluginPackage(string registrationName, string packageId)
    {
        Entity? existing = _queries.GetPluginPackageByName(registrationName);
        if (existing is not null
            || string.Equals(registrationName, packageId, StringComparison.OrdinalIgnoreCase))
        {
            return existing;
        }

        existing = _queries.GetPluginPackageByName(packageId);
        if (existing is not null)
        {
            _trace.WriteLine(
                "Found existing plugin package under NuGet id '{0}' (expected '{1}'). Updating content only — name cannot be changed after create.",
                packageId,
                registrationName);
        }

        return existing;
    }

    /// <summary>
    /// Before package content update, delete steps/images/secure config/Custom APIs for plugintypes
    /// that will no longer exist in the package. Dataverse deletes those plugintypes during update
    /// and fails with "Unable to delete … plugintype due to N step(s) registered on it" otherwise.
    /// </summary>
    private void RemoveDependenciesForTypesLeavingPackage(
        Guid packageEntityId,
        IReadOnlyDictionary<string, HashSet<string>> expectedTypesByAssembly)
    {
        foreach (Entity assembly in _queries.GetPluginAssembliesForPackage(packageEntityId))
        {
            string? assemblyName = assembly.GetAttributeValue<string>(PluginAssembly.Fields.Name);
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                continue;
            }

            expectedTypesByAssembly.TryGetValue(assemblyName, out HashSet<string>? expectedTypes);
            expectedTypes ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Entity existingType in _queries.GetPluginTypes(assembly.Id, isWorkflowActivity: false))
            {
                string? typeName = existingType.GetAttributeValue<string>(PluginType.Fields.TypeName);
                if (string.IsNullOrWhiteSpace(typeName) || expectedTypes.Contains(typeName))
                {
                    continue;
                }

                _trace.WriteLine(
                    "Plugin type '{0}' is leaving package assembly '{1}' — removing steps and related registrations before package update",
                    typeName,
                    assemblyName);
                DeletePluginTypeDependencies(existingType.Id, typeName);
            }
        }
    }

    /// <summary>
    /// When a solution is selected for deploy, builds the Dataverse package name as
    /// <c>{publisherPrefix}_{nugetPackageId}</c> from that solution's publisher.
    /// </summary>
    private string ResolvePluginPackageRegistrationName(string packageId)
    {
        if (string.IsNullOrWhiteSpace(SolutionUniqueName))
        {
            return packageId;
        }

        // Solution must exist first so its publisher (and customization prefix) can be resolved.
        EnsureSolutionExists();

        if (!_publisherPrefixBySolution.TryGetValue(SolutionUniqueName, out string? prefix))
        {
            prefix = _queries.GetPublisherCustomizationPrefix(SolutionUniqueName);
            _publisherPrefixBySolution[SolutionUniqueName] = prefix;
        }

        return PluginPackageNameResolver.ResolveRegistrationName(packageId, prefix);
    }

    private Guid UpsertPluginPackage(
        string packageId,
        string registrationName,
        string packageVersion,
        string packagePath,
        Entity? existing)
    {
        // === PACKAGE REGISTRATION (.nupkg) ===
        // 1. The ENTIRE .NUPKG file (not individual DLLs) is read and base64-encoded.
        // 2. Uploaded into pluginpackage.content .
        // 3. Dataverse (server-side) processes the package:
        //    - Extracts contained assemblies.
        //    - Creates pluginassembly records linked via packageid (sourcetype typically 4).
        //    - Creates corresponding plugintype records.
        // 4. Client then locally extracts the nupkg (see NuGetPackageReader) only to discover
        //    plugin types via reflection for step registration.
        // 5. Steps are registered against the server-created assemblies (resolved by packageid).
        //
        // Registration name: when plugins[].solution is set, name/uniquename become
        // {publisherCustomizationPrefix}_{NuGetPackageId} (e.g. contoso_Sample.Plugins).
        // version is required on create (from nuspec); name/version are immutable after create.
        //
        // Before content update, callers must remove steps/Custom APIs for plugintypes leaving
        // the package — Dataverse deletes those types during update and fails if steps remain.
        string content = Convert.ToBase64String(File.ReadAllBytes(packagePath));

        EnsureSolutionExists();

        if (existing is null)
        {
            _trace.WriteLine("Registering plugin package '{0}' v{1}", registrationName, packageVersion);
            Entity record = new Entity(PluginPackage.EntityLogicalName)
            {
                ["name"] = registrationName,
                ["uniquename"] = registrationName,
                ["version"] = packageVersion,
                ["content"] = content
            };

            return DataverseOrganizationRequests.CreateWithSolution(_service, record, SolutionUniqueName);
        }

        string existingName = existing.GetAttributeValue<string>(PluginPackage.Fields.Name)
            ?? existing.GetAttributeValue<string>(PluginPackage.Fields.UniqueName)
            ?? registrationName;
        _trace.WriteLine("Updating plugin package '{0}'", existingName);
        Entity update = new Entity(PluginPackage.EntityLogicalName, existing.Id)
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
                    StringComparison.OrdinalIgnoreCase));

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

    /// <summary>
    /// Waits until Dataverse has registered plugintypes from the uploaded package content.
    /// Package assemblies must not receive client-created plugintype rows.
    /// </summary>
    private Dictionary<string, Entity> WaitForPackagePluginTypes(
        Guid assemblyId,
        string assemblyName,
        IReadOnlyCollection<string> expectedTypeNames)
    {
        const int maxAttempts = 15;
        const int delaySeconds = 2;

        Dictionary<string, Entity> latest = new(StringComparer.OrdinalIgnoreCase);

        // Empty expected set: package has no IPlugin types for this assembly (all removed).
        // Do not wait for types that will never appear.
        if (expectedTypeNames.Count == 0)
        {
            latest = _queries.GetPluginTypes(assemblyId, isWorkflowActivity: false)
                .Where(record => !string.IsNullOrWhiteSpace(record.GetAttributeValue<string>("typename")))
                .ToDictionary(
                    record => record.GetAttributeValue<string>("typename")!,
                    record => record,
                    StringComparer.OrdinalIgnoreCase);
            _trace.WriteLine(
                "Package assembly '{0}' has no expected plugin types ({1} still on server)",
                assemblyName,
                latest.Count);
            return latest;
        }

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            latest = _queries.GetPluginTypes(assemblyId, isWorkflowActivity: false)
                .Where(record => !string.IsNullOrWhiteSpace(record.GetAttributeValue<string>("typename")))
                .ToDictionary(
                    record => record.GetAttributeValue<string>("typename")!,
                    record => record,
                    StringComparer.OrdinalIgnoreCase);

            int matched = expectedTypeNames.Count(name => latest.ContainsKey(name));
            if (matched == expectedTypeNames.Count)
            {
                _trace.WriteLine(
                    "Package assembly '{0}' has {1} plugin type(s) ready",
                    assemblyName,
                    latest.Count);
                return latest;
            }

            if (attempt < maxAttempts)
            {
                _trace.WriteLine(
                    "Waiting for Dataverse plugin types on assembly '{0}' ({1}/{2} expected, attempt {3}/{4})...",
                    assemblyName,
                    matched,
                    expectedTypeNames.Count,
                    attempt,
                    maxAttempts);
                Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
            }
        }

        string expected = string.Join(
            ", ",
            expectedTypeNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
        string found = latest.Count == 0
            ? "<none>"
            : string.Join(", ", latest.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));

        throw new PluginRegistrationException(
            $"Plugin assembly '{assemblyName}' did not expose expected plugintypes after package upload " +
            $"(expected: {expected}; found on server: {found}). " +
            "For plugin packages, Dataverse registers plugintypes from .nupkg content. " +
            "Verify the package includes the plugin DLL under lib/, types implement IPlugin, " +
            "and the project targets a supported .NET Framework (e.g. net462).");
    }

    private void RemoveOrphanedPluginTypes(IEnumerable<Type> pluginTypes, Guid assemblyId, bool isWorkflowActivity)
    {
        HashSet<string?> typeNames = pluginTypes.Select(t => t.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<Entity> existingTypes = _queries.GetPluginTypes(assemblyId, isWorkflowActivity);

        foreach (Entity existingType in existingTypes)
        {
            string? typeName = existingType.GetAttributeValue<string>(PluginType.Fields.TypeName);
            if (typeNames.Contains(typeName))
            {
                continue;
            }

            _trace.WriteLine("Removing orphaned plugin type '{0}'", typeName);
            DeletePluginTypeDependencies(existingType.Id, typeName);
            _service.Delete(PluginType.EntityLogicalName, existingType.Id);
        }
    }

    /// <summary>
    /// Deletes Custom APIs, steps (with images and secure config) registered against a plugintype.
    /// Does not delete the plugintype itself.
    /// </summary>
    private void DeletePluginTypeDependencies(Guid pluginTypeId, string? typeName)
    {
        if (!string.IsNullOrWhiteSpace(typeName))
        {
            foreach (CustomApiDetails customApi in _queries.GetCustomApisForPluginType(typeName))
            {
                DeleteCustomApiTree(customApi);
            }
        }

        foreach (Entity step in _queries.GetPluginSteps(pluginTypeId))
        {
            DeleteStepCompletely(step);
        }
    }

    private void DeleteCustomApiTree(CustomApiDetails customApi)
    {
        string uniqueName = customApi.Api.GetAttributeValue<string>(CustomAPI.Fields.UniqueName)
            ?? customApi.Api.Id.ToString();

        foreach (Entity parameter in customApi.RequestParameters)
        {
            _trace.WriteLine(
                "Deleting Custom API request parameter '{0}'",
                parameter.GetAttributeValue<string>("uniquename"));
            _service.Delete(CustomAPIRequestParameter.EntityLogicalName, parameter.Id);
        }

        foreach (Entity property in customApi.ResponseProperties)
        {
            _trace.WriteLine(
                "Deleting Custom API response property '{0}'",
                property.GetAttributeValue<string>("uniquename"));
            _service.Delete(CustomAPIResponseProperty.EntityLogicalName, property.Id);
        }

        _trace.WriteLine("Deleting Custom API '{0}'", uniqueName);
        _service.Delete(CustomAPI.EntityLogicalName, customApi.Api.Id);
    }

    private void DeleteStepCompletely(Entity step)
    {
        string? stepName = step.GetAttributeValue<string>(SdkMessageProcessingStep.Fields.Name);
        _trace.WriteLine("Deleting step '{0}'", stepName);

        foreach (Entity image in _queries.GetPluginStepImages(step.Id))
        {
            _trace.WriteLine(
                "Deleting image '{0}' for step '{1}'",
                image.GetAttributeValue<string>(SdkMessageProcessingStepImage.Fields.Name),
                stepName);
            _service.Delete(SdkMessageProcessingStepImage.EntityLogicalName, image.Id);
        }

        // Secure config is linked from the step; clear the lookup then delete the config row.
        Entity stepWithSecure = _service.Retrieve(
            SdkMessageProcessingStep.EntityLogicalName,
            step.Id,
            new ColumnSet(SdkMessageProcessingStep.Fields.SdkMessageProcessingStepSecureConfigId));
        EntityReference? secureRef = stepWithSecure.GetAttributeValue<EntityReference>(
            SdkMessageProcessingStep.Fields.SdkMessageProcessingStepSecureConfigId);
        if (secureRef is not null)
        {
            Entity clearLink = new Entity(SdkMessageProcessingStep.EntityLogicalName, step.Id)
            {
                [SdkMessageProcessingStep.Fields.SdkMessageProcessingStepSecureConfigId] = null
            };
            _service.Update(clearLink);
            _service.Delete(SdkMessageProcessingStepSecureConfig.EntityLogicalName, secureRef.Id);
        }

        _service.Delete(SdkMessageProcessingStep.EntityLogicalName, step.Id);
    }

    /// <summary>
    /// Registers steps/Custom APIs against plugintypes that Dataverse created from the package.
    /// Does not create or update <c>plugintype</c> rows.
    /// </summary>
    private void RegisterPluginStepsFromPackage(
        IEnumerable<Type> pluginTypes,
        Dictionary<string, Entity> serverTypes)
    {
        foreach (var pluginType in pluginTypes)
        {
            var attributeData = ReflectionHelper.GetRegistrationAttributes(pluginType).ToList();
            var customApiData = ReflectionHelper.GetCustomApiRegistrationAttributes(pluginType).ToList();

            if (attributeData.Count == 0 && customApiData.Count == 0)
            {
                continue;
            }

            if (!serverTypes.TryGetValue(pluginType.FullName!, out Entity? serverType))
            {
                throw new PluginRegistrationException(
                    $"Plugin type '{pluginType.FullName}' was not registered by Dataverse from the package. " +
                    "Ensure the type implements IPlugin and is included in the uploaded .nupkg.");
            }

            Guid pluginTypeId = serverType.Id;
            _trace.WriteLine("Using package plugin type '{0}'", pluginType.FullName);
            var existingSteps = _queries.GetPluginSteps(pluginTypeId);

            foreach (var data in attributeData)
            {
                var attribute = AttributeParser.Parse(data);
                if (!AttributeParser.IsPluginStepRegistration(attribute))
                {
                    continue;
                }

                var stepAttribute = PluginStepNameResolver.ApplyStepName(pluginType, attribute);
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

    private void RegisterCustomApi(Type pluginType, Guid pluginTypeId, CustomApiRegistration attribute)
    {
        var model = CustomApiAttributeReader.Read(pluginType, attribute);
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

        var record = step is null ? new Entity(SdkMessageProcessingStep.EntityLogicalName) : new Entity(SdkMessageProcessingStep.EntityLogicalName, step.Id);

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
        record["mode"] = new OptionSetValue(pluginStep.ExecutionMode == ExecutionModeEnum.Asynchronous ? 1 : 0);
        record["asyncautodelete"] = pluginStep.ExecutionMode == ExecutionModeEnum.Asynchronous && pluginStep.DeleteAsyncOperation;
        record["rank"] = pluginStep.ExecutionOrder;
        record["stage"] = new OptionSetValue((int)pluginStep.Stage!.Value);
        record["supporteddeployment"] = new OptionSetValue(GetSupportedDeployment(pluginStep));
        record["plugintypeid"] = new EntityReference(PluginType.EntityLogicalName, pluginTypeId);
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

        RegisterImages(registeredStepId, pluginType, pluginStep);

        if (!string.IsNullOrWhiteSpace(SolutionUniqueName))
        {
            AddComponentToSolution(SolutionUniqueName, 92, registeredStepId);
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
            _service.Delete(SdkMessageProcessingStepImage.EntityLogicalName, image.Id);
        }
    }

    private void RegisterImage(
        Guid stepId,
        PluginRegistrationAttribute pluginStep,
        List<Entity> existingImages,
        string? imageName,
        ImageTypeEnum imageType,
        string[] attributes)
    {
        if (string.IsNullOrWhiteSpace(imageName))
        {
            return;
        }

        var image = existingImages.FirstOrDefault(i =>
                String.Equals(i.GetAttributeValue<string>("entityalias"), imageName, StringComparison.Ordinal)
                && i.GetAttributeValue<OptionSetValue>("imagetype")?.Value == (int)imageType)
            ?? new Entity(SdkMessageProcessingStepImage.EntityLogicalName);

        image["name"] = imageName;
        image["entityalias"] = imageName;
        image["imagetype"] = new OptionSetValue((int)imageType);
        image["attributes"] = NormalizeCommaSeparated(attributes ?? []);
        image["sdkmessageprocessingstepid"] = new EntityReference(SdkMessageProcessingStep.EntityLogicalName, stepId);
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

    private static string GetImageMessagePropertyName(string message) => message switch
    {
        "Create" => "Id",
        "SetState" or "SetStateDynamicEntity" => "EntityMoniker",
        "Send" or "DeliverIncoming" or "DeliverPromote" => "EmailId",
        _ => "Target"
    };

    private void AddComponentToSolution(string solutionName, int componentType, Guid componentId, bool addRequiredComponents = false)
    {
        EnsureSolutionExists();
        _trace.WriteLine("Adding component to solution '{0}'", solutionName);
        DataverseOrganizationRequests.AddSolutionComponent(
            _service,
            solutionName,
            componentType,
            componentId,
            addRequiredComponents);
    }

    private void EnsureSolutionExists()
        => _solutionEnsure.EnsureExists(SolutionUniqueName);
}
