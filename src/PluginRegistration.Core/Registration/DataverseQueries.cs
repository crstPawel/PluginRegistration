using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

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
        var query = new QueryExpression("pluginassembly")
        {
            ColumnSet = new ColumnSet("pluginassemblyid", "name", "packageid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("name", ConditionOperator.Equal, name)
                }
            },
            TopCount = 1
        };

        return _service.RetrieveMultiple(query).Entities.FirstOrDefault();
    }

    public Entity? GetPluginPackageByName(string name)
    {
        QueryExpression query = new QueryExpression("pluginpackage")
        {
            ColumnSet = new ColumnSet("pluginpackageid", "name", "uniquename"),
            Criteria = new FilterExpression(LogicalOperator.Or)
            {
                Conditions =
                {
                    new ConditionExpression("name", ConditionOperator.Equal, name),
                    new ConditionExpression("uniquename", ConditionOperator.Equal, name)
                }
            },
            TopCount = 1
        };

        return _service.RetrieveMultiple(query).Entities.FirstOrDefault();
    }

    public List<Entity> GetPluginAssembliesForPackage(Guid packageId)
    {
        QueryExpression query = new QueryExpression("pluginassembly")
        {
            ColumnSet = new ColumnSet("pluginassemblyid", "name"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("packageid", ConditionOperator.Equal, packageId)
                }
            },
            Orders = { new OrderExpression("name", OrderType.Ascending) }
        };

        return _service.RetrieveMultiple(query).Entities.ToList();
    }

    public List<Entity> GetPluginTypes(Guid assemblyId, bool? isWorkflowActivity = null)
    {
        var query = new QueryExpression("plugintype")
        {
            ColumnSet = new ColumnSet(
                "plugintypeid",
                "name",
                "typename",
                "friendlyname",
                "description",
                "workflowactivitygroupname",
                "isworkflowactivity"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("pluginassemblyid", ConditionOperator.Equal, assemblyId)
                }
            }
        };

        if (isWorkflowActivity.HasValue)
        {
            query.Criteria.AddCondition("isworkflowactivity", ConditionOperator.Equal, isWorkflowActivity.Value);
        }

        return _service.RetrieveMultiple(query).Entities.ToList();
    }

    public List<Entity> GetPluginSteps(Guid pluginTypeId)
    {
        var query = new QueryExpression("sdkmessageprocessingstep")
        {
            ColumnSet = new ColumnSet(
                "sdkmessageprocessingstepid",
                "name",
                "mode",
                "rank",
                "stage",
                "configuration",
                "description",
                "filteringattributes",
                "asyncautodelete",
                "supporteddeployment",
                "sdkmessageid",
                "sdkmessagefilterid",
                "plugintypeid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("plugintypeid", ConditionOperator.Equal, pluginTypeId)
                }
            }
        };

        return _service.RetrieveMultiple(query).Entities.ToList();
    }

    public List<Entity> GetPluginStepImages(Guid stepId)
    {
        var query = new QueryExpression("sdkmessageprocessingstepimage")
        {
            ColumnSet = new ColumnSet(
                "sdkmessageprocessingstepimageid",
                "name",
                "entityalias",
                "imagetype",
                "attributes",
                "messagepropertyname"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("sdkmessageprocessingstepid", ConditionOperator.Equal, stepId)
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
        var query = new QueryExpression("customapi")
        {
            ColumnSet = new ColumnSet(
                "customapiid",
                "uniquename",
                "name",
                "displayname",
                "description",
                "bindingtype",
                "boundentitylogicalname",
                "isfunction",
                "isprivate",
                "allowedcustomprocessingsteptype",
                "plugintypeid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("uniquename", ConditionOperator.Equal, uniqueName)
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
        var query = new QueryExpression("customapirequestparameter")
        {
            ColumnSet = new ColumnSet(
                "customapirequestparameterid",
                "uniquename",
                "name",
                "displayname",
                "description",
                "type",
                "isoptional",
                "logicalentityname"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("customapiid", ConditionOperator.Equal, customApiId)
                }
            }
        };

        return _service.RetrieveMultiple(query).Entities.ToList();
    }

    public List<Entity> GetCustomApiResponseProperties(Guid customApiId)
    {
        var query = new QueryExpression("customapiresponseproperty")
        {
            ColumnSet = new ColumnSet(
                "customapiresponsepropertyid",
                "uniquename",
                "name",
                "displayname",
                "description",
                "type",
                "logicalentityname"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("customapiid", ConditionOperator.Equal, customApiId)
                }
            }
        };

        return _service.RetrieveMultiple(query).Entities.ToList();
    }

    public List<CustomApiDetails> GetCustomApisForPluginType(string typeName)
    {
        var query = new QueryExpression("customapi")
        {
            ColumnSet = new ColumnSet(
                "customapiid",
                "uniquename",
                "name",
                "displayname",
                "description",
                "bindingtype",
                "boundentitylogicalname",
                "isfunction",
                "isprivate",
                "allowedcustomprocessingsteptype",
                "plugintypeid"),
            LinkEntities =
            {
                new LinkEntity("customapi", "plugintype", "plugintypeid", "plugintypeid", JoinOperator.Inner)
                {
                    LinkCriteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("typename", ConditionOperator.Equal, typeName)
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
        var query = new QueryExpression("plugintype")
        {
            ColumnSet = new ColumnSet(
                "plugintypeid",
                "typename",
                "name",
                "friendlyname",
                "description",
                "workflowactivitygroupname",
                "pluginassemblyid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("typename", ConditionOperator.Equal, typeName)
                }
            },
            TopCount = 1
        };

        return _service.RetrieveMultiple(query).Entities.FirstOrDefault();
    }

    public OptionSetValue? GetAssemblyIsolationMode(Guid pluginTypeId)
    {
        var pluginType = _service.Retrieve("plugintype", pluginTypeId, new ColumnSet("pluginassemblyid"));
        var assemblyRef = pluginType.GetAttributeValue<EntityReference>("pluginassemblyid");
        var assembly = _service.Retrieve("pluginassembly", assemblyRef.Id, new ColumnSet("isolationmode"));
        return assembly.GetAttributeValue<OptionSetValue>("isolationmode");
    }
}
