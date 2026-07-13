using Microsoft.Xrm.Sdk;
using PluginRegistration.Attributes;
using PluginRegistration.Core.Config;
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
    private string? _solutionUniqueName;

    public CustomApiRegistrationService(IOrganizationService service, ITrace trace)
    {
        _service = service;
        _queries = new DataverseQueries(service);
        _trace = trace;
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

    public void EnsureCustomApis(IEnumerable<CustomApiDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.PluginTypeName))
            {
                _trace.WriteLine(
                    "Skipping profile Custom API '{0}' because pluginTypeName is not set.",
                    definition.UniqueName);
                continue;
            }

            var pluginType = _queries.GetPluginTypeByTypeName(definition.PluginTypeName);
            if (pluginType is null)
            {
                throw new PluginRegistrationException(
                    $"Plugin type '{definition.PluginTypeName}' must be deployed before Custom API '{definition.UniqueName}' (createIfMissing). " +
                    "Make sure the assembly is built, the 'assemblyPath' in pluginregistration.json points to the correct folder containing the DLL, " +
                    "and the plugin class was registered in this (or a previous) deploy.");
            }

            RegisterCustomApi(CustomApiAttributeReader.FromProfileDefinition(definition), pluginType.Id);
        }
    }

    private void CreateCustomApiTree(CustomApiRegistrationModel model, Guid pluginTypeId)
    {
        var record = BuildCustomApiEntity(model, pluginTypeId);
        _trace.WriteLine("Creating Custom API '{0}'", model.UniqueName);
        var customApiId = _service.Create(record);

        foreach (var parameter in model.RequestParameters)
        {
            var parameterId = CreateRequestParameter(customApiId, parameter);
            AddComponentToSolution(SolutionComponentTypes.CustomApiRequestParameter, parameterId);
        }

        foreach (var property in model.ResponseProperties)
        {
            var propertyId = CreateResponseProperty(customApiId, property);
            AddComponentToSolution(SolutionComponentTypes.CustomApiResponseProperty, propertyId);
        }

        AddComponentToSolution(SolutionComponentTypes.CustomApi, customApiId, addRequiredComponents: true);
    }

    private void UpdateCustomApi(
        CustomApiDetails existing,
        CustomApiRegistrationModel model,
        Guid pluginTypeId)
    {
        var update = new Entity(CustomAPI.EntityLogicalName, existing.Api.Id)
        {
            ["displayname"] = model.DisplayName,
            ["description"] = model.Description,
            ["isprivate"] = model.IsPrivate,
            ["allowedcustomprocessingsteptype"] = new OptionSetValue((int)model.AllowedCustomProcessingStepType),
            ["plugintypeid"] = new EntityReference(PluginType.EntityLogicalName, pluginTypeId)
        };

        _trace.WriteLine("Updating Custom API '{0}'", model.UniqueName);
        _service.Update(update);

        SyncRequestParameters(existing, model);
        SyncResponseProperties(existing, model);

        AddComponentToSolution(SolutionComponentTypes.CustomApi, existing.Api.Id, addRequiredComponents: true);
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
                UpdateRequestParameter(current.Id, parameter);
                AddComponentToSolution(SolutionComponentTypes.CustomApiRequestParameter, current.Id);
                continue;
            }

            var parameterId = CreateRequestParameter(existing.Api.Id, parameter);
            AddComponentToSolution(SolutionComponentTypes.CustomApiRequestParameter, parameterId);
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
                AddComponentToSolution(SolutionComponentTypes.CustomApiResponseProperty, current.Id);
                continue;
            }

            var propertyId = CreateResponseProperty(existing.Api.Id, property);
            AddComponentToSolution(SolutionComponentTypes.CustomApiResponseProperty, propertyId);
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
        var record = new Entity(CustomAPI.EntityLogicalName)
        {
            ["uniquename"] = model.UniqueName,
            ["name"] = model.UniqueName,
            ["displayname"] = model.DisplayName,
            ["description"] = model.Description,
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
        var record = new Entity(CustomAPIRequestParameter.EntityLogicalName)
        {
            ["customapiid"] = new EntityReference(CustomAPI.EntityLogicalName, customApiId),
            ["uniquename"] = parameter.UniqueName,
            ["name"] = parameter.UniqueName,
            ["displayname"] = parameter.DisplayName,
            ["description"] = parameter.Description,
            ["type"] = new OptionSetValue((int)parameter.Type),
            ["isoptional"] = !parameter.IsRequired
        };

        if (!string.IsNullOrWhiteSpace(parameter.EntityLogicalName))
        {
            record["logicalentityname"] = parameter.EntityLogicalName;
        }

        _trace.WriteLine("Creating Custom API request parameter '{0}'", parameter.UniqueName);
        return _service.Create(record);
    }

    private void UpdateRequestParameter(Guid parameterId, CustomApiParameterModel parameter)
    {
        var record = new Entity(CustomAPIRequestParameter.EntityLogicalName, parameterId)
        {
            ["displayname"] = parameter.DisplayName,
            ["description"] = parameter.Description,
            ["isoptional"] = !parameter.IsRequired
        };

        _trace.WriteLine("Updating Custom API request parameter '{0}'", parameter.UniqueName);
        _service.Update(record);
    }

    private Guid CreateResponseProperty(Guid customApiId, CustomApiParameterModel property)
    {
        var record = new Entity(CustomAPIResponseProperty.EntityLogicalName)
        {
            ["customapiid"] = new EntityReference(CustomAPI.EntityLogicalName, customApiId),
            ["uniquename"] = property.UniqueName,
            ["name"] = property.UniqueName,
            ["displayname"] = property.DisplayName,
            ["description"] = property.Description,
            ["type"] = new OptionSetValue((int)property.Type)
        };

        if (!string.IsNullOrWhiteSpace(property.EntityLogicalName))
        {
            record["logicalentityname"] = property.EntityLogicalName;
        }

        _trace.WriteLine("Creating Custom API response property '{0}'", property.UniqueName);
        return _service.Create(record);
    }

    private void UpdateResponseProperty(Guid propertyId, CustomApiParameterModel property)
    {
        var record = new Entity(CustomAPIResponseProperty.EntityLogicalName, propertyId)
        {
            ["displayname"] = property.DisplayName,
            ["description"] = property.Description
        };

        _trace.WriteLine("Updating Custom API response property '{0}'", property.UniqueName);
        _service.Update(record);
    }

    private void AddComponentToSolution(int componentType, Guid componentId, bool addRequiredComponents = false)
    {
        if (string.IsNullOrWhiteSpace(SolutionUniqueName))
        {
            return;
        }

        _trace.WriteLine("Adding Custom API component to solution '{0}'", SolutionUniqueName);
        DataverseOrganizationRequests.AddSolutionComponent(
            _service,
            SolutionUniqueName,
            componentType,
            componentId,
            addRequiredComponents);
    }
}