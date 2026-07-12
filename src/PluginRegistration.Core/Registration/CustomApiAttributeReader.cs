using System.Reflection;
using PluginRegistration.Attributes;
using PluginRegistration.Core.Config;

namespace PluginRegistration.Core.Registration;

public static class CustomApiAttributeReader
{
    public static CustomApiRegistrationModel Read(
        Type pluginType,
        PluginRegistrationAttribute attribute,
        CustomApiDefinition? profileOverride = null)
    {
        if (string.IsNullOrWhiteSpace(attribute.Message))
        {
            throw new PluginRegistrationException(
                $"Custom API unique name is required on type '{pluginType.FullName}'.");
        }

        var customApiCount = ReflectionHelper.GetRegistrationAttributes(pluginType)
            .Select(AttributeParser.Parse)
            .Count(AttributeParser.IsCustomApiRegistration);
        var hasMultipleCustomApis = customApiCount > 1;

        var requestParameters = pluginType.GetCustomAttributesData()
            .Where(data => data.AttributeType.Name == nameof(CustomApiRequestParameterAttribute))
            .Select(ParseRequestParameter)
            .Where(parameter => MatchesCustomApi(parameter.ApiUniqueName, attribute.Message!, hasMultipleCustomApis, pluginType))
            .OrderBy(parameter => parameter.UniqueName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var responseProperties = pluginType.GetCustomAttributesData()
            .Where(data => data.AttributeType.Name == nameof(CustomApiResponsePropertyAttribute))
            .Select(ParseResponseProperty)
            .Where(property => MatchesCustomApi(property.ApiUniqueName, attribute.Message!, hasMultipleCustomApis, pluginType))
            .OrderBy(property => property.UniqueName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ValidateUniqueNames(requestParameters, responseProperties, pluginType.FullName!);

        var model = new CustomApiRegistrationModel
        {
            UniqueName = attribute.Message,
            PluginTypeName = pluginType.FullName!,
            DisplayName = string.IsNullOrWhiteSpace(attribute.FriendlyName)
                ? attribute.Message
                : attribute.FriendlyName,
            Description = attribute.Description,
            BindingType = attribute.CustomApiBindingType,
            IsFunction = attribute.IsFunction,
            IsPrivate = attribute.IsPrivate,
            BoundEntityLogicalName = attribute.BoundEntityLogicalName,
            AllowedCustomProcessingStepType = attribute.AllowedCustomProcessingStepType,
            RequestParameters = requestParameters,
            ResponseProperties = responseProperties
        };

        return ApplyProfileOverride(model, profileOverride);
    }

    public static CustomApiRegistrationModel FromProfileDefinition(CustomApiDefinition definition)
    {
        return new CustomApiRegistrationModel
        {
            UniqueName = definition.UniqueName,
            PluginTypeName = definition.PluginTypeName ?? string.Empty,
            DisplayName = string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.UniqueName
                : definition.DisplayName,
            Description = definition.Description,
            BindingType = (CustomApiBindingTypeEnum)definition.BindingType,
            IsFunction = definition.IsFunction,
            IsPrivate = definition.IsPrivate,
            BoundEntityLogicalName = definition.BoundEntityLogicalName,
            AllowedCustomProcessingStepType = (CustomApiProcessingStepTypeEnum)definition.AllowedCustomProcessingStepType,
            RequestParameters = definition.RequestParameters
                .Select(parameter => new CustomApiParameterModel
                {
                    UniqueName = parameter.UniqueName,
                    Type = (CustomApiParameterTypeEnum)parameter.Type,
                    DisplayName = string.IsNullOrWhiteSpace(parameter.DisplayName)
                        ? parameter.UniqueName
                        : parameter.DisplayName,
                    Description = parameter.Description,
                    IsRequired = parameter.IsRequired,
                    EntityLogicalName = parameter.EntityLogicalName
                })
                .ToList(),
            ResponseProperties = definition.ResponseProperties
                .Select(property => new CustomApiParameterModel
                {
                    UniqueName = property.UniqueName,
                    Type = (CustomApiParameterTypeEnum)property.Type,
                    DisplayName = string.IsNullOrWhiteSpace(property.DisplayName)
                        ? property.UniqueName
                        : property.DisplayName,
                    Description = property.Description,
                    EntityLogicalName = property.EntityLogicalName
                })
                .ToList()
        };
    }

    private static CustomApiRegistrationModel ApplyProfileOverride(
        CustomApiRegistrationModel model,
        CustomApiDefinition? profileOverride)
    {
        if (profileOverride is null)
        {
            return model;
        }

        return new CustomApiRegistrationModel
        {
            UniqueName = model.UniqueName,
            PluginTypeName = string.IsNullOrWhiteSpace(profileOverride.PluginTypeName)
                ? model.PluginTypeName
                : profileOverride.PluginTypeName,
            DisplayName = string.IsNullOrWhiteSpace(profileOverride.DisplayName)
                ? model.DisplayName
                : profileOverride.DisplayName,
            Description = profileOverride.Description ?? model.Description,
            BindingType = model.BindingType,
            IsFunction = model.IsFunction,
            IsPrivate = model.IsPrivate,
            BoundEntityLogicalName = model.BoundEntityLogicalName,
            AllowedCustomProcessingStepType = model.AllowedCustomProcessingStepType,
            RequestParameters = model.RequestParameters,
            ResponseProperties = model.ResponseProperties
        };
    }

    private static CustomApiParameterModel ParseRequestParameter(CustomAttributeData data)
    {
        var arguments = data.ConstructorArguments.ToArray();
        var model = new CustomApiParameterModel
        {
            UniqueName = (string)arguments[0].Value!,
            Type = (CustomApiParameterTypeEnum)Enum.ToObject(
                typeof(CustomApiParameterTypeEnum),
                (int)arguments[1].Value!),
            DisplayName = (string)arguments[0].Value!
        };

        foreach (var namedArgument in data.NamedArguments)
        {
            switch (namedArgument.MemberName)
            {
                case nameof(CustomApiRequestParameterAttribute.DisplayName):
                    model = model with
                    {
                        DisplayName = (string?)namedArgument.TypedValue.Value ?? model.DisplayName
                    };
                    break;
                case nameof(CustomApiRequestParameterAttribute.Description):
                    model = model with { Description = (string?)namedArgument.TypedValue.Value };
                    break;
                case nameof(CustomApiRequestParameterAttribute.IsRequired):
                    model = model with { IsRequired = (bool)namedArgument.TypedValue.Value! };
                    break;
                case nameof(CustomApiRequestParameterAttribute.EntityLogicalName):
                    model = model with { EntityLogicalName = (string?)namedArgument.TypedValue.Value };
                    break;
                case nameof(CustomApiRequestParameterAttribute.ApiUniqueName):
                    model = model with { ApiUniqueName = (string?)namedArgument.TypedValue.Value };
                    break;
            }
        }

        return model;
    }

    private static CustomApiParameterModel ParseResponseProperty(CustomAttributeData data)
    {
        var arguments = data.ConstructorArguments.ToArray();
        var model = new CustomApiParameterModel
        {
            UniqueName = (string)arguments[0].Value!,
            Type = (CustomApiParameterTypeEnum)Enum.ToObject(
                typeof(CustomApiParameterTypeEnum),
                (int)arguments[1].Value!),
            DisplayName = (string)arguments[0].Value!
        };

        foreach (var namedArgument in data.NamedArguments)
        {
            switch (namedArgument.MemberName)
            {
                case nameof(CustomApiResponsePropertyAttribute.DisplayName):
                    model = model with
                    {
                        DisplayName = (string?)namedArgument.TypedValue.Value ?? model.DisplayName
                    };
                    break;
                case nameof(CustomApiResponsePropertyAttribute.Description):
                    model = model with { Description = (string?)namedArgument.TypedValue.Value };
                    break;
                case nameof(CustomApiResponsePropertyAttribute.EntityLogicalName):
                    model = model with { EntityLogicalName = (string?)namedArgument.TypedValue.Value };
                    break;
                case nameof(CustomApiResponsePropertyAttribute.ApiUniqueName):
                    model = model with { ApiUniqueName = (string?)namedArgument.TypedValue.Value };
                    break;
            }
        }

        return model;
    }

    private static bool MatchesCustomApi(
        string? parameterApiUniqueName,
        string apiUniqueName,
        bool hasMultipleCustomApis,
        Type pluginType)
    {
        if (!string.IsNullOrWhiteSpace(parameterApiUniqueName))
        {
            return string.Equals(parameterApiUniqueName, apiUniqueName, StringComparison.OrdinalIgnoreCase);
        }

        if (hasMultipleCustomApis)
        {
            throw new PluginRegistrationException(
                $"ApiUniqueName is required on Custom API request/response attributes when type '{pluginType.FullName}' registers multiple Custom APIs.");
        }

        return true;
    }

    private static void ValidateUniqueNames(
        IReadOnlyCollection<CustomApiParameterModel> requestParameters,
        IReadOnlyCollection<CustomApiParameterModel> responseProperties,
        string pluginTypeName)
    {
        var duplicateRequests = requestParameters
            .GroupBy(parameter => parameter.UniqueName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateRequests.Count > 0)
        {
            throw new PluginRegistrationException(
                $"Duplicate Custom API request parameter names on '{pluginTypeName}': {string.Join(", ", duplicateRequests)}");
        }

        var duplicateResponses = responseProperties
            .GroupBy(property => property.UniqueName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateResponses.Count > 0)
        {
            throw new PluginRegistrationException(
                $"Duplicate Custom API response property names on '{pluginTypeName}': {string.Join(", ", duplicateResponses)}");
        }

        var overlap = requestParameters
            .Select(parameter => parameter.UniqueName)
            .Intersect(responseProperties.Select(property => property.UniqueName), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (overlap.Count > 0)
        {
            throw new PluginRegistrationException(
                $"Custom API request/response name collision on '{pluginTypeName}': {string.Join(", ", overlap)}");
        }
    }
}