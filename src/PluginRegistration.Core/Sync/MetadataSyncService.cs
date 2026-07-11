using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using PluginRegistration.Attributes;
using PluginRegistration.Core.Registration;

namespace PluginRegistration.Core.Sync;

/// <summary>
/// Downloads plugin step metadata from Dataverse and writes CrmPluginRegistration attributes into source code.
/// </summary>
public sealed class MetadataSyncService
{
    private const int CustomApiInternalStage = 30;

    private readonly IOrganizationService _service;
    private readonly DataverseQueries _queries;
    private readonly ITrace _trace;

    public MetadataSyncService(IOrganizationService service, ITrace trace)
    {
        _service = service;
        _queries = new DataverseQueries(service);
        _trace = trace;
    }

    public void SyncSourceCode(string sourceDirectory, string? classRegex = null)
    {
        SourceCodeTypeIndex? typeIndex = null;
        if (string.IsNullOrWhiteSpace(classRegex))
        {
            typeIndex = SourceCodeTypeIndex.Build(sourceDirectory);
            _trace.WriteLine(
                "Indexed {0} plugin type(s) and {1} workflow type(s) using inheritance analysis.",
                typeIndex.PluginTypeCount,
                typeIndex.WorkflowTypeCount);
        }

        var files = Directory.EnumerateFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        var updatedFiles = 0;
        foreach (var file in files)
        {
            CodeParser parser;
            if (typeIndex is not null)
            {
                if (!typeIndex.HasPluginOrWorkflowTypes(file))
                {
                    continue;
                }

                parser = new CodeParser(
                    file,
                    typeIndex.GetPluginTypesInFile(file),
                    typeIndex.GetWorkflowTypesInFile(file),
                    typeIndex);
            }
            else
            {
                parser = new CodeParser(file, classRegex);
                if (parser.PluginCount == 0)
                {
                    continue;
                }
            }

            parser.RemoveExistingAttributes();

            foreach (var className in parser.ClassNames)
            {
                if (parser.IsPlugin(className))
                {
                    AddPluginAttributes(parser, className);
                }
                else if (parser.IsWorkflowActivity(className))
                {
                    AddWorkflowAttributes(parser, className);
                }
            }

            parser.Save();
            updatedFiles++;
            _trace.WriteLine("Updated source file '{0}'", file);
        }

        _trace.WriteLine("Decorated {0} source file(s) with registration attributes.", updatedFiles);
    }

    private void AddPluginAttributes(CodeParser parser, string className)
    {
        var steps = _queries.GetPluginStepsForTypeName(className);
        var duplicateNames = steps
            .Select(s => s.GetAttributeValue<string>("name"))
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.Skip(1))
            .ToList();

        if (duplicateNames.Count > 0)
        {
            throw new PluginRegistrationException(
                $"Duplicate step names found for plugin type {className}: {string.Join(", ", duplicateNames)}");
        }

        foreach (var step in steps)
        {
            var stage = step.GetAttributeValue<OptionSetValue>("stage")?.Value ?? 0;
            if (stage == CustomApiInternalStage)
            {
                continue;
            }

            var messageRef = step.GetAttributeValue<EntityReference>("sdkmessageid");
            var messageName = _queries.GetMessageName(messageRef.Id)!;
            string? entityLogicalName = "none";
            var filterRef = step.GetAttributeValue<EntityReference>("sdkmessagefilterid");
            if (filterRef is not null)
            {
                entityLogicalName = _queries.GetEntityLogicalName(filterRef.Id);
            }

            var stageEnum = (StageEnum)step.GetAttributeValue<OptionSetValue>("stage")!.Value;
            var stepName = step.GetAttributeValue<string>("name")!;
            var defaultStepName = PluginStepNameResolver.Resolve(className, stageEnum);
            var attribute = new CrmPluginRegistrationAttribute(
                messageName,
                entityLogicalName!,
                stageEnum,
                step.GetAttributeValue<OptionSetValue>("mode")?.Value == 1
                    ? ExecutionModeEnum.Asynchronous
                    : ExecutionModeEnum.Synchronous,
                step.GetAttributeValue<string>("filteringattributes"),
                step.GetAttributeValue<int?>("rank") ?? 1)
            {
                Id = step.Id.ToString(),
                DeleteAsyncOperation = step.GetAttributeValue<bool?>("asyncautodelete") ?? false,
                Description = step.GetAttributeValue<string>("description"),
                UnSecureConfiguration = step.GetAttributeValue<string>("configuration")
            };

            if (!string.Equals(stepName, defaultStepName, StringComparison.Ordinal))
            {
                attribute.Name = stepName;
            }

            var stepImages = ReadStepImages(step.Id);
            parser.AddAttribute(attribute, className);
            parser.AddStepImageAttributes(stageEnum, messageName, stepImages, className);
        }

        AddCustomApiAttributes(parser, className);
    }

    private void AddCustomApiAttributes(CodeParser parser, string className)
    {
        var customApis = _queries.GetCustomApisForPluginType(className).ToList();
        var hasMultipleCustomApis = customApis.Count > 1;

        foreach (var details in customApis)
        {
            var api = details.Api;
            var attribute = new CrmPluginRegistrationAttribute(api.GetAttributeValue<string>("uniquename")!)
            {
                FriendlyName = api.GetAttributeValue<string>("displayname"),
                Description = api.GetAttributeValue<string>("description"),
                CustomApiBindingType = (CustomApiBindingTypeEnum)(api.GetAttributeValue<OptionSetValue>("bindingtype")?.Value ?? 0),
                BoundEntityLogicalName = api.GetAttributeValue<string>("boundentitylogicalname"),
                IsFunction = api.GetAttributeValue<bool>("isfunction"),
                IsPrivate = api.GetAttributeValue<bool>("isprivate"),
                AllowedCustomProcessingStepType = (CustomApiProcessingStepTypeEnum)(api.GetAttributeValue<OptionSetValue>("allowedcustomprocessingsteptype")?.Value ?? 0)
            };

            var apiUniqueName = api.GetAttributeValue<string>("uniquename")!;
            var requestParameters = details.RequestParameters
                .Select(parameter => new CustomApiParameterModel
                {
                    UniqueName = parameter.GetAttributeValue<string>("uniquename")!,
                    DisplayName = parameter.GetAttributeValue<string>("displayname") ?? parameter.GetAttributeValue<string>("uniquename")!,
                    Description = parameter.GetAttributeValue<string>("description"),
                    Type = (CustomApiParameterTypeEnum)(parameter.GetAttributeValue<OptionSetValue>("type")?.Value ?? 10),
                    IsRequired = !parameter.GetAttributeValue<bool>("isoptional"),
                    EntityLogicalName = parameter.GetAttributeValue<string>("logicalentityname"),
                    ApiUniqueName = hasMultipleCustomApis ? apiUniqueName : null
                })
                .ToList();

            var responseProperties = details.ResponseProperties
                .Select(property => new CustomApiParameterModel
                {
                    UniqueName = property.GetAttributeValue<string>("uniquename")!,
                    DisplayName = property.GetAttributeValue<string>("displayname") ?? property.GetAttributeValue<string>("uniquename")!,
                    Description = property.GetAttributeValue<string>("description"),
                    Type = (CustomApiParameterTypeEnum)(property.GetAttributeValue<OptionSetValue>("type")?.Value ?? 10),
                    EntityLogicalName = property.GetAttributeValue<string>("logicalentityname"),
                    ApiUniqueName = hasMultipleCustomApis ? apiUniqueName : null
                })
                .ToList();

            parser.AddCustomApiAttributes(attribute, requestParameters, responseProperties, className);
        }
    }

    private List<PluginStepImageModel> ReadStepImages(Guid stepId)
    {
        return _queries.GetPluginStepImages(stepId)
            .Select(image => new PluginStepImageModel
            {
                Name = image.GetAttributeValue<string>("entityalias") ?? image.GetAttributeValue<string>("name") ?? string.Empty,
                ImageType = (ImageTypeEnum)(image.GetAttributeValue<OptionSetValue>("imagetype")?.Value ?? 0),
                Attributes = image.GetAttributeValue<string>("attributes")
            })
            .ToList();
    }

    private void AddWorkflowAttributes(CodeParser parser, string className)
    {
        var pluginType = _queries.GetPluginTypeByTypeName(className);
        if (pluginType is null)
        {
            return;
        }

        var isolationMode = _queries.GetAssemblyIsolationMode(pluginType.Id);
        var attribute = new CrmPluginRegistrationAttribute(
            pluginType.GetAttributeValue<string>("name")!,
            pluginType.GetAttributeValue<string>("friendlyname")!,
            pluginType.GetAttributeValue<string>("description") ?? string.Empty,
            pluginType.GetAttributeValue<string>("workflowactivitygroupname") ?? string.Empty,
            isolationMode?.Value == 2 ? IsolationModeEnum.Sandbox : IsolationModeEnum.None);

        parser.AddAttribute(attribute, className);
    }
}