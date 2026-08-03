using Microsoft.Xrm.Sdk;
using PluginRegistration.Attributes;
using PluginRegistration.Core.Connection;
using PluginRegistration.Core.Model.Entities;

namespace PluginRegistration.Core.Registration;

/// <summary>
/// Creates and maintains Custom API definitions, request parameters, response properties,
/// and plugin type bindings based on code attributes.
/// </summary>
public sealed class CustomApiRegistrationService
{
    private readonly IOrganizationService _service;
    private readonly DataverseQueries _queries;
    private readonly ITrace _trace;
    private readonly SolutionEnsureService _solutionEnsure;
    private readonly SolutionComponentTypeResolver _componentTypes;
    private readonly Dictionary<string, string> _publisherPrefixBySolution = new(StringComparer.OrdinalIgnoreCase);
    private string? _solutionUniqueName;

    public CustomApiRegistrationService(IOrganizationService service, ITrace trace)
    {
        _service = service;
        _queries = new DataverseQueries(service);
        _trace = trace;
        _solutionEnsure = new SolutionEnsureService(service, trace);
        _componentTypes = new SolutionComponentTypeResolver(service);
    }

    public string? SolutionUniqueName
    {
        get => _solutionUniqueName;
        set => _solutionUniqueName = value;
    }

    public void RegisterCustomApi(CustomApiRegistrationModel model, Guid pluginTypeId)
    {
        if (string.IsNullOrWhiteSpace(model.UniqueName))
        {
            throw new PluginRegistrationException("Custom API uniqueName is required.");
        }

        // Dataverse requires customapi / parameter uniquename to start with a valid publisher
        // customization prefix. When plugins[].solution is set, use that solution's publisher
        // (not a hard-coded / default prefix).
        model = ApplySolutionPublisherPrefix(model);

        _trace.WriteLine(
            "Registering Custom API '{0}' from attributes/model ({1} request parameter(s), {2} response propert(y/ies))",
            model.UniqueName,
            model.RequestParameters.Count,
            model.ResponseProperties.Count);

        var existing = _queries.GetCustomApiDetails(model.UniqueName);
        if (existing is null)
        {
            CreateCustomApiTree(model, pluginTypeId);
            return;
        }

        if (RequiresRecreate(existing, model))
        {
            _trace.WriteLine(
                "Custom API '{0}' has immutable changes. Recreating definition and parameters.",
                model.UniqueName);
            DeleteCustomApiTree(existing.Api.Id);
            CreateCustomApiTree(model, pluginTypeId);
            return;
        }

        UpdateCustomApi(existing, model, pluginTypeId);
    }

    private void CreateCustomApiTree(CustomApiRegistrationModel model, Guid pluginTypeId)
    {
        EnsureSolutionReady();

        var record = BuildCustomApiEntity(model, pluginTypeId);
        _trace.WriteLine(
            "Creating Custom API '{0}' ({1} request parameter(s), {2} response propert(y/ies))",
            model.UniqueName,
            model.RequestParameters.Count,
            model.ResponseProperties.Count);

        // Solution-aware tables: Create with SolutionUniqueName (official path).
        // Component types 371/372 are Connectors — never use those for customapi.
        var customApiId = DataverseOrganizationRequests.CreateWithSolution(
            _service,
            record,
            SolutionUniqueName);

        foreach (var parameter in model.RequestParameters)
        {
            CreateRequestParameter(customApiId, parameter);
        }

        foreach (var property in model.ResponseProperties)
        {
            CreateResponseProperty(customApiId, property);
        }

        // Ensure API + children are in the solution (OTC from metadata + required components).
        AddCustomApiWithRequiredComponentsToSolution(customApiId, model.UniqueName);
    }

    private void UpdateCustomApi(
        CustomApiDetails existing,
        CustomApiRegistrationModel model,
        Guid pluginTypeId)
    {
        string displayName = ResolveDisplayName(model.DisplayName, model.UniqueName);
        var update = new Entity(CustomAPI.EntityLogicalName, existing.Api.Id)
        {
            ["displayname"] = displayName,
            ["description"] = ResolveDescription(model.Description, displayName, model.UniqueName),
            ["isprivate"] = model.IsPrivate,
            ["allowedcustomprocessingsteptype"] = new OptionSetValue((int)model.AllowedCustomProcessingStepType),
            ["plugintypeid"] = new EntityReference(PluginType.EntityLogicalName, pluginTypeId)
        };

        _trace.WriteLine("Updating Custom API '{0}'", model.UniqueName);
        DataverseOrganizationRequests.UpdateWithSolution(_service, update, SolutionUniqueName);

        SyncRequestParameters(existing, model);
        SyncResponseProperties(existing, model);

        // After parameters/properties exist, re-add Custom API with required components so new
        // children land in the solution (GUI: Add required objects).
        AddCustomApiWithRequiredComponentsToSolution(existing.Api.Id, model.UniqueName);
    }

    private void SyncRequestParameters(CustomApiDetails existing, CustomApiRegistrationModel model)
    {
        var desired = model.RequestParameters.ToDictionary(
            parameter => parameter.UniqueName,
            parameter => parameter,
            StringComparer.OrdinalIgnoreCase);

        foreach (var current in existing.RequestParameters)
        {
            var uniqueName = current.GetAttributeValue<string>("uniquename")!;
            if (!desired.ContainsKey(uniqueName))
            {
                _trace.WriteLine("Deleting Custom API request parameter '{0}'", uniqueName);
                _service.Delete(CustomAPIRequestParameter.EntityLogicalName, current.Id);
            }
        }

        var refreshed = _queries.GetCustomApiRequestParameters(existing.Api.Id)
            .ToDictionary(
                parameter => parameter.GetAttributeValue<string>("uniquename")!,
                parameter => parameter,
                StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in model.RequestParameters)
        {
            if (refreshed.TryGetValue(parameter.UniqueName, out var current))
            {
                // Update only — do not re-run AddSolutionComponent on every deploy.
                // AddSolutionComponent for type 371 can fail with msdyn_Connector MetadataCache
                // on some environments even when AddRequiredComponents=false.
                UpdateRequestParameter(current.Id, parameter);
                continue;
            }

            _trace.WriteLine(
                "Creating missing Custom API request parameter '{0}' on '{1}'",
                parameter.UniqueName,
                model.UniqueName);
            CreateRequestParameter(existing.Api.Id, parameter);
        }
    }

    private void SyncResponseProperties(CustomApiDetails existing, CustomApiRegistrationModel model)
    {
        var desired = model.ResponseProperties.ToDictionary(
            property => property.UniqueName,
            property => property,
            StringComparer.OrdinalIgnoreCase);

        foreach (var current in existing.ResponseProperties)
        {
            var uniqueName = current.GetAttributeValue<string>("uniquename")!;
            if (!desired.ContainsKey(uniqueName))
            {
                _trace.WriteLine("Deleting Custom API response property '{0}'", uniqueName);
                _service.Delete(CustomAPIResponseProperty.EntityLogicalName, current.Id);
            }
        }

        var refreshed = _queries.GetCustomApiResponseProperties(existing.Api.Id)
            .ToDictionary(
                property => property.GetAttributeValue<string>("uniquename")!,
                property => property,
                StringComparer.OrdinalIgnoreCase);

        foreach (var property in model.ResponseProperties)
        {
            if (refreshed.TryGetValue(property.UniqueName, out var current))
            {
                UpdateResponseProperty(current.Id, property);
                continue;
            }

            _trace.WriteLine(
                "Creating missing Custom API response property '{0}' on '{1}'",
                property.UniqueName,
                model.UniqueName);
            CreateResponseProperty(existing.Api.Id, property);
        }
    }

    private static bool RequiresRecreate(CustomApiDetails existing, CustomApiRegistrationModel model)
    {
        var api = existing.Api;

        if (!string.Equals(api.GetAttributeValue<string>("uniquename"), model.UniqueName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (api.GetAttributeValue<OptionSetValue>("bindingtype")?.Value != (int)model.BindingType)
        {
            return true;
        }

        if (api.GetAttributeValue<bool>("isfunction") != model.IsFunction)
        {
            return true;
        }

        if (!string.Equals(
                api.GetAttributeValue<string>("boundentitylogicalname"),
                model.BoundEntityLogicalName,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (HasImmutableParameterChanges(existing.RequestParameters, model.RequestParameters, isRequest: true))
        {
            return true;
        }

        return HasImmutableParameterChanges(existing.ResponseProperties, model.ResponseProperties, isRequest: false);
    }

    private static bool HasImmutableParameterChanges(
        IReadOnlyCollection<Entity> existingParameters,
        IReadOnlyCollection<CustomApiParameterModel> desiredParameters,
        bool isRequest)
    {
        var existingByName = existingParameters.ToDictionary(
            parameter => parameter.GetAttributeValue<string>("uniquename")!,
            parameter => parameter,
            StringComparer.OrdinalIgnoreCase);

        foreach (var desired in desiredParameters)
        {
            if (!existingByName.TryGetValue(desired.UniqueName, out var current))
            {
                continue;
            }

            if (current.GetAttributeValue<OptionSetValue>("type")?.Value != (int)desired.Type)
            {
                return true;
            }

            if (!string.Equals(
                    current.GetAttributeValue<string>("logicalentityname"),
                    desired.EntityLogicalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (isRequest
                && current.GetAttributeValue<bool>("isoptional") != !desired.IsRequired)
            {
                return true;
            }
        }

        return false;
    }

    private void DeleteCustomApiTree(Guid customApiId)
    {
        foreach (var parameter in _queries.GetCustomApiRequestParameters(customApiId))
        {
            _trace.WriteLine(
                "Deleting Custom API request parameter '{0}'",
                parameter.GetAttributeValue<string>("uniquename"));
            _service.Delete(CustomAPIRequestParameter.EntityLogicalName, parameter.Id);
        }

        foreach (var property in _queries.GetCustomApiResponseProperties(customApiId))
        {
            _trace.WriteLine(
                "Deleting Custom API response property '{0}'",
                property.GetAttributeValue<string>("uniquename"));
            _service.Delete(CustomAPIResponseProperty.EntityLogicalName, property.Id);
        }

        _trace.WriteLine("Deleting Custom API '{0}'", customApiId);
        _service.Delete(CustomAPI.EntityLogicalName, customApiId);
    }

    private static Entity BuildCustomApiEntity(CustomApiRegistrationModel model, Guid pluginTypeId)
    {
        string displayName = ResolveDisplayName(model.DisplayName, model.UniqueName);
        var record = new Entity(CustomAPI.EntityLogicalName)
        {
            ["uniquename"] = model.UniqueName,
            ["name"] = model.UniqueName,
            ["displayname"] = displayName,
            ["description"] = ResolveDescription(model.Description, displayName, model.UniqueName),
            ["bindingtype"] = new OptionSetValue((int)model.BindingType),
            ["isfunction"] = model.IsFunction,
            ["isprivate"] = model.IsPrivate,
            ["allowedcustomprocessingsteptype"] = new OptionSetValue((int)model.AllowedCustomProcessingStepType),
            ["plugintypeid"] = new EntityReference(PluginType.EntityLogicalName, pluginTypeId)
        };

        if (!string.IsNullOrWhiteSpace(model.BoundEntityLogicalName))
        {
            record["boundentitylogicalname"] = model.BoundEntityLogicalName;
        }

        return record;
    }

    private Guid CreateRequestParameter(Guid customApiId, CustomApiParameterModel parameter)
    {
        string displayName = ResolveDisplayName(parameter.DisplayName, parameter.UniqueName);
        var record = new Entity(CustomAPIRequestParameter.EntityLogicalName)
        {
            ["customapiid"] = new EntityReference(CustomAPI.EntityLogicalName, customApiId),
            ["uniquename"] = parameter.UniqueName,
            ["name"] = parameter.UniqueName,
            ["displayname"] = displayName,
            ["description"] = ResolveDescription(parameter.Description, displayName, parameter.UniqueName),
            ["type"] = new OptionSetValue((int)parameter.Type),
            ["isoptional"] = !parameter.IsRequired
        };

        if (!string.IsNullOrWhiteSpace(parameter.EntityLogicalName))
        {
            record["logicalentityname"] = parameter.EntityLogicalName;
        }

        _trace.WriteLine("Creating Custom API request parameter '{0}'", parameter.UniqueName);
        return DataverseOrganizationRequests.CreateWithSolution(_service, record, SolutionUniqueName);
    }

    private void UpdateRequestParameter(Guid parameterId, CustomApiParameterModel parameter)
    {
        string displayName = ResolveDisplayName(parameter.DisplayName, parameter.UniqueName);
        var record = new Entity(CustomAPIRequestParameter.EntityLogicalName, parameterId)
        {
            ["displayname"] = displayName,
            ["description"] = ResolveDescription(parameter.Description, displayName, parameter.UniqueName),
            ["isoptional"] = !parameter.IsRequired
        };

        _trace.WriteLine("Updating Custom API request parameter '{0}'", parameter.UniqueName);
        DataverseOrganizationRequests.UpdateWithSolution(_service, record, SolutionUniqueName);
    }

    private Guid CreateResponseProperty(Guid customApiId, CustomApiParameterModel property)
    {
        string displayName = ResolveDisplayName(property.DisplayName, property.UniqueName);
        var record = new Entity(CustomAPIResponseProperty.EntityLogicalName)
        {
            ["customapiid"] = new EntityReference(CustomAPI.EntityLogicalName, customApiId),
            ["uniquename"] = property.UniqueName,
            ["name"] = property.UniqueName,
            ["displayname"] = displayName,
            ["description"] = ResolveDescription(property.Description, displayName, property.UniqueName),
            ["type"] = new OptionSetValue((int)property.Type)
        };

        if (!string.IsNullOrWhiteSpace(property.EntityLogicalName))
        {
            record["logicalentityname"] = property.EntityLogicalName;
        }

        _trace.WriteLine("Creating Custom API response property '{0}'", property.UniqueName);
        return DataverseOrganizationRequests.CreateWithSolution(_service, record, SolutionUniqueName);
    }

    private void UpdateResponseProperty(Guid propertyId, CustomApiParameterModel property)
    {
        string displayName = ResolveDisplayName(property.DisplayName, property.UniqueName);
        var record = new Entity(CustomAPIResponseProperty.EntityLogicalName, propertyId)
        {
            ["displayname"] = displayName,
            ["description"] = ResolveDescription(property.Description, displayName, property.UniqueName)
        };

        _trace.WriteLine("Updating Custom API response property '{0}'", property.UniqueName);
        DataverseOrganizationRequests.UpdateWithSolution(_service, record, SolutionUniqueName);
    }

    /// <summary>
    /// Dataverse requires non-null description on Custom API creates. When the attribute omits
    /// Description, fall back to DisplayName (then unique name).
    /// </summary>
    private static string ResolveDescription(string? description, string? displayName, string uniqueName)
    {
        if (!string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        return ResolveDisplayName(displayName, uniqueName);
    }

    private static string ResolveDisplayName(string? displayName, string uniqueName)
        => string.IsNullOrWhiteSpace(displayName) ? uniqueName : displayName;

    /// <summary>
    /// Mirrors maker UI: select Custom API → Add to solution → include required components.
    /// Uses the <c>customapi</c> table ObjectTypeCode (not classic picklist 372, which is Connector).
    /// </summary>
    private void AddCustomApiWithRequiredComponentsToSolution(Guid customApiId, string uniqueName)
    {
        if (string.IsNullOrWhiteSpace(SolutionUniqueName))
        {
            return;
        }

        EnsureSolutionReady();
        int customApiComponentType = _componentTypes.CustomApi;
        _trace.WriteLine(
            "Adding Custom API '{0}' ({1}) to solution '{2}' as component type {3} with required components",
            uniqueName,
            customApiId,
            SolutionUniqueName,
            customApiComponentType);

        try
        {
            DataverseOrganizationRequests.AddSolutionComponent(
                _service,
                SolutionUniqueName,
                customApiComponentType,
                customApiId,
                addRequiredComponents: true);
            return;
        }
        catch (Exception ex)
        {
            _trace.WriteLine(
                "Warning: Add required components for Custom API '{0}' failed: {1}. Retrying parent only…",
                uniqueName,
                ex.Message);
        }

        try
        {
            DataverseOrganizationRequests.AddSolutionComponent(
                _service,
                SolutionUniqueName,
                customApiComponentType,
                customApiId,
                addRequiredComponents: false);
            _trace.WriteLine(
                "Warning: Custom API '{0}' was added to solution '{1}' without required components. " +
                "Add missing parameters/properties via solution explorer if needed.",
                uniqueName,
                SolutionUniqueName);
        }
        catch (Exception ex)
        {
            _trace.WriteLine(
                "Warning: could not add Custom API '{0}' ({1}) to solution '{2}': {3}. " +
                "The Custom API record was saved in the environment.",
                uniqueName,
                customApiId,
                SolutionUniqueName,
                ex.Message);
        }
    }

    /// <summary>
    /// Applies the solution publisher customization prefix to Custom API and parameter unique names
    /// when <see cref="SolutionUniqueName"/> is set. Skips double-prefixing if the name already
    /// starts with <c>{prefix}_</c>.
    /// </summary>
    private CustomApiRegistrationModel ApplySolutionPublisherPrefix(CustomApiRegistrationModel model)
    {
        string? prefix = ResolveSolutionPublisherPrefix();
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return model;
        }

        string resolvedApiName = PluginPackageNameResolver.ResolveRegistrationName(model.UniqueName, prefix);
        if (!string.Equals(resolvedApiName, model.UniqueName, StringComparison.Ordinal))
        {
            _trace.WriteLine(
                "Custom API unique name '{0}' resolved with solution publisher prefix to '{1}'",
                model.UniqueName,
                resolvedApiName);
        }

        // Only the Custom API export key (uniquename) is prefixed. Request/response parameter
        // unique names are the keys used in InputParameters/OutputParameters — leave them as
        // authored so plugin code keeps working.
        return new CustomApiRegistrationModel
        {
            UniqueName = resolvedApiName,
            PluginTypeName = model.PluginTypeName,
            DisplayName = model.DisplayName,
            Description = model.Description,
            BindingType = model.BindingType,
            IsFunction = model.IsFunction,
            IsPrivate = model.IsPrivate,
            BoundEntityLogicalName = model.BoundEntityLogicalName,
            AllowedCustomProcessingStepType = model.AllowedCustomProcessingStepType,
            RequestParameters = model.RequestParameters,
            ResponseProperties = model.ResponseProperties
        };
    }

    private string? ResolveSolutionPublisherPrefix()
    {
        if (string.IsNullOrWhiteSpace(SolutionUniqueName))
        {
            return null;
        }

        EnsureSolutionReady();

        if (!_publisherPrefixBySolution.TryGetValue(SolutionUniqueName, out string? prefix))
        {
            prefix = _queries.GetPublisherCustomizationPrefix(SolutionUniqueName);
            _publisherPrefixBySolution[SolutionUniqueName] = prefix;
            _trace.WriteLine(
                "Using publisher customization prefix '{0}' from solution '{1}' for Custom API unique names",
                prefix,
                SolutionUniqueName);
        }

        return prefix;
    }

    private void EnsureSolutionReady()
        => _solutionEnsure.EnsureExists(SolutionUniqueName);
}