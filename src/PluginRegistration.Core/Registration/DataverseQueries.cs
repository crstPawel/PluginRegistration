using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using PluginRegistration.Core.Model.Entities;

namespace PluginRegistration.Core.Registration;

public sealed class DataverseQueries
{
    private readonly IOrganizationService _service;

    public DataverseQueries(IOrganizationService service)
    {
        _service = service;
    }

    public Entity? GetPluginAssemblyByName(string name)
    {
        var query = new QueryExpression(PluginAssembly.EntityLogicalName)
        {
            ColumnSet = new ColumnSet(
                PluginAssembly.Fields.Id,
                PluginAssembly.Fields.Name,
                PluginAssembly.Fields.PackageId,
                PluginAssembly.Fields.IsolationMode),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(PluginAssembly.Fields.Name, ConditionOperator.Equal, name)
                }
            },
            TopCount = 1
        };

        return _service.RetrieveMultiple(query).Entities.FirstOrDefault();
    }

    public Entity? GetPluginPackageByName(string name)
    {
        QueryExpression query = new QueryExpression(PluginPackage.EntityLogicalName)
        {
            ColumnSet = new ColumnSet(PluginPackage.Fields.PluginPackageId, PluginPackage.Fields.Name, PluginPackage.Fields.UniqueName),
            Criteria = new FilterExpression(LogicalOperator.Or)
            {
                Conditions =
                {
                    new ConditionExpression(PluginPackage.Fields.Name, ConditionOperator.Equal, name),
                    new ConditionExpression(PluginPackage.Fields.UniqueName, ConditionOperator.Equal, name)
                }
            },
            TopCount = 1
        };

        return _service.RetrieveMultiple(query).Entities.FirstOrDefault();
    }

    public List<Entity> GetPluginAssembliesForPackage(Guid packageId)
    {
        QueryExpression query = new QueryExpression(PluginAssembly.EntityLogicalName)
        {
            ColumnSet = new ColumnSet(PluginAssembly.Fields.PluginAssemblyId, PluginAssembly.Fields.Name),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(PluginAssembly.Fields.PackageId, ConditionOperator.Equal, packageId)
                }
            },
            Orders = { new OrderExpression(PluginAssembly.Fields.Name, OrderType.Ascending) }
        };

        return _service.RetrieveMultiple(query).Entities.ToList();
    }

    public List<Entity> GetPluginTypes(Guid assemblyId, bool? isWorkflowActivity = null)
    {
        var query = new QueryExpression(PluginType.EntityLogicalName)
        {
            ColumnSet = new ColumnSet(
                PluginType.Fields.PluginTypeId,
                PluginType.Fields.Name,
                PluginType.Fields.TypeName,
                PluginType.Fields.FriendlyName,
                PluginType.Fields.Description,
                PluginType.Fields.WorkflowActivityGroupName,
                PluginType.Fields.IsWorkflowActivity),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(PluginType.Fields.PluginAssemblyId, ConditionOperator.Equal, assemblyId)
                }
            }
        };

        if (isWorkflowActivity.HasValue)
        {
            query.Criteria.AddCondition(PluginType.Fields.IsWorkflowActivity, ConditionOperator.Equal, isWorkflowActivity.Value);
        }

        return _service.RetrieveMultiple(query).Entities.ToList();
    }

    public List<Entity> GetPluginSteps(Guid pluginTypeId)
    {
        var query = new QueryExpression(SdkMessageProcessingStep.EntityLogicalName)
        {
            ColumnSet = new ColumnSet(
                SdkMessageProcessingStep.Fields.SdkMessageProcessingStepId,
                SdkMessageProcessingStep.Fields.Name,
                SdkMessageProcessingStep.Fields.Mode,
                SdkMessageProcessingStep.Fields.Rank,
                SdkMessageProcessingStep.Fields.Stage,
                SdkMessageProcessingStep.Fields.Configuration,
                SdkMessageProcessingStep.Fields.Description,
                SdkMessageProcessingStep.Fields.FilteringAttributes,
                SdkMessageProcessingStep.Fields.AsyncAutoDelete,
                SdkMessageProcessingStep.Fields.SupportedDeployment,
                SdkMessageProcessingStep.Fields.SdkMessageId,
                SdkMessageProcessingStep.Fields.SdkMessageFilterId,
                SdkMessageProcessingStep.Fields.PluginTypeId),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(SdkMessageProcessingStep.Fields.PluginTypeId, ConditionOperator.Equal, pluginTypeId)
                }
            }
        };

        return _service.RetrieveMultiple(query).Entities.ToList();
    }

    public List<Entity> GetPluginStepImages(Guid stepId)
    {
        var query = new QueryExpression(SdkMessageProcessingStepImage.EntityLogicalName)
        {
            ColumnSet = new ColumnSet(
                SdkMessageProcessingStepImage.Fields.SdkMessageProcessingStepImageId,
                SdkMessageProcessingStepImage.Fields.Name,
                SdkMessageProcessingStepImage.Fields.EntityAlias,
                SdkMessageProcessingStepImage.Fields.ImageType,
                SdkMessageProcessingStepImage.Fields.Attributes1,
                SdkMessageProcessingStepImage.Fields.MessagePropertyName),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(SdkMessageProcessingStepImage.Fields.SdkMessageProcessingStepId, ConditionOperator.Equal, stepId)
                }
            }
        };

        return _service.RetrieveMultiple(query).Entities.ToList();
    }

    public Guid? GetMessageId(string messageName)
    {
        var query = new QueryExpression("sdkmessage")
        {
            ColumnSet = new ColumnSet("sdkmessageid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("name", ConditionOperator.Equal, messageName)
                }
            },
            TopCount = 1
        };

        var message = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
        return message?.Id;
    }

    public (Guid MessageId, Guid FilterId)? GetMessageFilter(string entityLogicalName, string messageName)
    {
        var query = new QueryExpression("sdkmessagefilter")
        {
            ColumnSet = new ColumnSet("sdkmessagefilterid", "sdkmessageid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("primaryobjecttypecode", ConditionOperator.Equal, entityLogicalName)
                }
            },
            LinkEntities =
            {
                new LinkEntity("sdkmessagefilter", "sdkmessage", "sdkmessageid", "sdkmessageid", JoinOperator.Inner)
                {
                    LinkCriteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("name", ConditionOperator.Equal, messageName)
                        }
                    }
                }
            },
            TopCount = 1
        };

        var filter = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
        if (filter is null)
        {
            return null;
        }

        return (filter.GetAttributeValue<EntityReference>("sdkmessageid").Id, filter.Id);
    }

    public string? GetEntityLogicalName(Guid messageFilterId)
    {
        var filter = _service.Retrieve("sdkmessagefilter", messageFilterId, new ColumnSet("primaryobjecttypecode"));
        return filter.GetAttributeValue<string>("primaryobjecttypecode");
    }

    public string? GetMessageName(Guid messageId)
    {
        var message = _service.Retrieve("sdkmessage", messageId, new ColumnSet("name"));
        return message.GetAttributeValue<string>("name");
    }

    public Entity? GetCustomApiByUniqueName(string uniqueName)
        => GetCustomApiDetails(uniqueName)?.Api;

    public CustomApiDetails? GetCustomApiDetails(string uniqueName)
    {
        var query = new QueryExpression(CustomAPI.EntityLogicalName)
        {
            ColumnSet = new ColumnSet(
                CustomAPI.Fields.CustomAPIId,
                CustomAPI.Fields.UniqueName,
                CustomAPI.Fields.Name,
                CustomAPI.Fields.DisplayName,
                CustomAPI.Fields.Description,
                CustomAPI.Fields.BindingType,
                CustomAPI.Fields.BoundEntityLogicalName,
                CustomAPI.Fields.IsFunction,
                CustomAPI.Fields.IsPrivate,
                CustomAPI.Fields.AllowedCustomProcessingStepType,
                CustomAPI.Fields.PluginTypeId),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(CustomAPI.Fields.UniqueName, ConditionOperator.Equal, uniqueName)
                }
            },
            TopCount = 1
        };

        var api = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
        if (api is null)
        {
            return null;
        }

        return new CustomApiDetails
        {
            Api = api,
            RequestParameters = GetCustomApiRequestParameters(api.Id),
            ResponseProperties = GetCustomApiResponseProperties(api.Id)
        };
    }

    public List<Entity> GetCustomApiRequestParameters(Guid customApiId)
    {
        var query = new QueryExpression(CustomAPIRequestParameter.EntityLogicalName)
        {
            ColumnSet = new ColumnSet(
                CustomAPIRequestParameter.Fields.CustomAPIRequestParameterId,
                CustomAPIRequestParameter.Fields.UniqueName,
                CustomAPIRequestParameter.Fields.Name,
                CustomAPIRequestParameter.Fields.DisplayName,
                CustomAPIRequestParameter.Fields.Description,
                CustomAPIRequestParameter.Fields.Type,
                CustomAPIRequestParameter.Fields.IsOptional,
                CustomAPIRequestParameter.Fields.LogicalEntityName),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(CustomAPIRequestParameter.Fields.CustomAPIId, ConditionOperator.Equal, customApiId)
                }
            }
        };

        return _service.RetrieveMultiple(query).Entities.ToList();
    }

    public List<Entity> GetCustomApiResponseProperties(Guid customApiId)
    {
        var query = new QueryExpression(CustomAPIResponseProperty.EntityLogicalName)
        {
            ColumnSet = new ColumnSet(
                CustomAPIResponseProperty.Fields.CustomAPIResponsePropertyId,
                CustomAPIResponseProperty.Fields.UniqueName,
                CustomAPIResponseProperty.Fields.Name,
                CustomAPIResponseProperty.Fields.DisplayName,
                CustomAPIResponseProperty.Fields.Description,
                CustomAPIResponseProperty.Fields.Type,
                CustomAPIResponseProperty.Fields.LogicalEntityName),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(CustomAPIResponseProperty.Fields.CustomAPIId, ConditionOperator.Equal, customApiId)
                }
            }
        };

        return _service.RetrieveMultiple(query).Entities.ToList();
    }

    public List<CustomApiDetails> GetCustomApisForPluginType(string typeName)
    {
        var query = new QueryExpression(CustomAPI.EntityLogicalName)
        {
            ColumnSet = new ColumnSet(
                CustomAPI.Fields.CustomAPIId,
                CustomAPI.Fields.UniqueName,
                CustomAPI.Fields.Name,
                CustomAPI.Fields.DisplayName,
                CustomAPI.Fields.Description,
                CustomAPI.Fields.BindingType,
                CustomAPI.Fields.BoundEntityLogicalName,
                CustomAPI.Fields.IsFunction,
                CustomAPI.Fields.IsPrivate,
                CustomAPI.Fields.AllowedCustomProcessingStepType,
                CustomAPI.Fields.PluginTypeId),
            LinkEntities =
            {
                new LinkEntity(CustomAPI.EntityLogicalName, PluginType.EntityLogicalName, CustomAPI.Fields.PluginTypeId, PluginType.Fields.PluginTypeId, JoinOperator.Inner)
                {
                    LinkCriteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression(PluginType.Fields.TypeName, ConditionOperator.Equal, typeName)
                        }
                    }
                }
            }
        };

        return _service.RetrieveMultiple(query).Entities
            .Select(api => new CustomApiDetails
            {
                Api = api,
                RequestParameters = GetCustomApiRequestParameters(api.Id),
                ResponseProperties = GetCustomApiResponseProperties(api.Id)
            })
            .ToList();
    }

    public List<Entity> GetPluginStepsForTypeName(string typeName)
    {
        var pluginType = GetPluginTypeByTypeName(typeName);
        if (pluginType is null)
        {
            return [];
        }

        return GetPluginSteps(pluginType.Id);
    }

    public Entity? GetPluginTypeByTypeName(string typeName)
    {
        var query = new QueryExpression(PluginType.EntityLogicalName)
        {
            ColumnSet = new ColumnSet(
                PluginType.Fields.PluginTypeId,
                PluginType.Fields.TypeName,
                PluginType.Fields.Name,
                PluginType.Fields.FriendlyName,
                PluginType.Fields.Description,
                PluginType.Fields.WorkflowActivityGroupName,
                PluginType.Fields.PluginAssemblyId),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(PluginType.Fields.TypeName, ConditionOperator.Equal, typeName)
                }
            },
            TopCount = 1
        };

        return _service.RetrieveMultiple(query).Entities.FirstOrDefault();
    }

    public OptionSetValue? GetAssemblyIsolationMode(Guid pluginTypeId)
    {
        var pluginType = _service.Retrieve(PluginType.EntityLogicalName, pluginTypeId, new ColumnSet(PluginType.Fields.PluginAssemblyId));
        var assemblyRef = pluginType.GetAttributeValue<EntityReference>(PluginType.Fields.PluginAssemblyId);
        var assembly = _service.Retrieve(PluginAssembly.EntityLogicalName, assemblyRef.Id, new ColumnSet(PluginAssembly.Fields.IsolationMode));
        return assembly.GetAttributeValue<OptionSetValue>(PluginAssembly.Fields.IsolationMode);
    }
}
