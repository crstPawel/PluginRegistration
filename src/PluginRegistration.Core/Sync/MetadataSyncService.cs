using Microsoft.Xrm.Sdk;
using PluginRegistration.Attributes;
using PluginRegistration.Core.Registration;

namespace PluginRegistration.Core.Sync;

/// <summary>
/// Downloads plugin step metadata from Dataverse and writes registration attributes into source code.
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
            var attribute = CreatePluginRegistrationAttribute(
                messageName,
                entityLogicalName!,
                stageEnum,
                step.GetAttributeValue<OptionSetValue>("mode")?.Value == 1
                    ? ExecutionModeEnum.Asynchronous
                    : ExecutionModeEnum.Synchronous,
                step.GetAttributeValue<string>("filteringattributes") ?? string.Empty,
                step.GetAttributeValue<int?>("rank") ?? 1);

            attribute.Id = step.Id.ToString();
            attribute.DeleteAsyncOperation = step.GetAttributeValue<bool?>("asyncautodelete") ?? false;
            attribute.UnSecureConfiguration = step.GetAttributeValue<string>("configuration");

            if (!string.Equals(stepName, defaultStepName, StringComparison.Ordinal))
            {
                attribute.Name = stepName;
            }

            var stepImages = ReadStepImages(step.Id);
            parser.AddAttribute(attribute, className);
            parser.AddStepImageAttributes(stepImages, className);
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
            var uniqueName = api.GetAttributeValue<string>("uniquename")!;
            var displayName = api.GetAttributeValue<string>("displayname") ?? uniqueName;
            var bindingType = (CustomApiBindingTypeEnum)(api.GetAttributeValue<OptionSetValue>("bindingtype")?.Value ?? 0);
            var processingStepType = (CustomApiProcessingStepTypeEnum)(api.GetAttributeValue<OptionSetValue>("allowedcustomprocessingsteptype")?.Value ?? 0);
            var boundEntityLogicalName = api.GetAttributeValue<string>("boundentitylogicalname") ?? string.Empty;

            CustomApiRegistration attribute;
            if (bindingType != CustomApiBindingTypeEnum.Global
                || processingStepType != CustomApiProcessingStepTypeEnum.None
                || !string.IsNullOrWhiteSpace(boundEntityLogicalName))
            {
                attribute = new CustomApiRegistration(
                    uniqueName,
                    displayName,
                    processingStepType,
                    bindingType,
                    boundEntityLogicalName);
            }
            else if (!string.Equals(displayName, uniqueName, StringComparison.Ordinal))
            {
                attribute = new CustomApiRegistration(uniqueName, displayName);
            }
            else
            {
                attribute = new CustomApiRegistration(uniqueName);
            }

            attribute.Description = api.GetAttributeValue<string>("description");
            attribute.IsFunction = api.GetAttributeValue<bool>("isfunction");
            attribute.IsPrivate = api.GetAttributeValue<bool>("isprivate");

            var requestParameters = details.RequestParameters
                .Select(parameter => new CustomApiParameterModel
                {
                    UniqueName = parameter.GetAttributeValue<string>("uniquename")!,
                    DisplayName = parameter.GetAttributeValue<string>("displayname") ?? parameter.GetAttributeValue<string>("uniquename")!,
                    Description = parameter.GetAttributeValue<string>("description"),
                    Type = (CustomApiParameterTypeEnum)(parameter.GetAttributeValue<OptionSetValue>("type")?.Value ?? 10),
                    IsRequired = !parameter.GetAttributeValue<bool>("isoptional"),
                    EntityLogicalName = parameter.GetAttributeValue<string>("logicalentityname"),
                    ApiUniqueName = hasMultipleCustomApis ? uniqueName : null
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
                    ApiUniqueName = hasMultipleCustomApis ? uniqueName : null
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
        _ = parser;
        _ = className;
        _trace.WriteLine(
            "Skipping workflow activity '{0}' - workflow registration attributes are no longer supported.",
            className);
    }

    private static PluginRegistrationAttribute CreatePluginRegistrationAttribute(
        string messageName,
        string entityLogicalName,
        StageEnum stage,
        ExecutionModeEnum executionMode,
        string filteringAttributes,
        int rank)
    {
        if (!Enum.TryParse<MessageTypeEnum>(messageName, true, out var messageType))
        {
            throw new PluginRegistrationException(
                $"Unknown message '{messageName}'. Add it to MessageTypeEnum before syncing this step.");
        }

        var filteringArray = string.IsNullOrWhiteSpace(filteringAttributes)
            ? []
            : filteringAttributes
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return PluginRegistrationAttribute.CreateStep(
            messageType,
            entityLogicalName,
            stage,
            executionMode,
            filteringArray,
            rank);
    }
}