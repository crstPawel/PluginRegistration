using System.Reflection;
using PluginRegistration.Attributes;

namespace PluginRegistration.Core.Registration;

public static class CustomApiAttributeReader
{
    public static CustomApiRegistrationModel Read(
        Type pluginType,
        CustomApiRegistration attribute)
    {
        if (string.IsNullOrWhiteSpace(attribute.UniqueName))
        {
            throw new PluginRegistrationException(
                $"Custom API unique name is required on type '{pluginType.FullName}'.");
        }

        var customApiCount = ReflectionHelper.GetCustomApiRegistrationAttributes(pluginType).Count();
        var hasMultipleCustomApis = customApiCount > 1;

        // Enumerate CustomAttributeData carefully — MetadataLoadContext can throw when
        // resolving AttributeType for some attributes; match by constructor declaring type too.
        var requestParameters = GetCustomAttributesDataSafe(pluginType)
            .Where(data => IsAttributeType(data, nameof(CustomApiRequestParameterAttribute)))
            .Select(ParseRequestParameter)
            .Where(parameter => MatchesCustomApi(parameter.ApiUniqueName, attribute.UniqueName, hasMultipleCustomApis, pluginType))
            .OrderBy(parameter => parameter.UniqueName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var responseProperties = GetCustomAttributesDataSafe(pluginType)
            .Where(data => IsAttributeType(data, nameof(CustomApiResponsePropertyAttribute)))
            .Select(ParseResponseProperty)
            .Where(property => MatchesCustomApi(property.ApiUniqueName, attribute.UniqueName, hasMultipleCustomApis, pluginType))
            .OrderBy(property => property.UniqueName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ValidateUniqueNames(requestParameters, responseProperties, pluginType.FullName!);

        return new CustomApiRegistrationModel
        {
            UniqueName = attribute.UniqueName,
            PluginTypeName = pluginType.FullName!,
            DisplayName = string.IsNullOrWhiteSpace(attribute.DisplayName)
                ? attribute.UniqueName
                : attribute.DisplayName,
            Description = attribute.Description,
            BindingType = attribute.CustomApiBindingType,
            IsFunction = attribute.IsFunction,
            IsPrivate = attribute.IsPrivate,
            BoundEntityLogicalName = attribute.BoundEntityLogicalName,
            AllowedCustomProcessingStepType = attribute.ProcessingStepType,
            RequestParameters = requestParameters,
            ResponseProperties = responseProperties
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

    private static IEnumerable<CustomAttributeData> GetCustomAttributesDataSafe(Type pluginType)
    {
        try
        {
            return pluginType.GetCustomAttributesData();
        }
        catch (FileNotFoundException)
        {
            return [];
        }
        catch (TypeLoadException)
        {
            return [];
        }
    }

    private static bool IsAttributeType(CustomAttributeData data, string attributeTypeName)
    {
        try
        {
            if (string.Equals(data.AttributeType.Name, attributeTypeName, StringComparison.Ordinal))
            {
                return true;
            }
        }
        catch (FileNotFoundException)
        {
            // Fall through to constructor declaring type.
        }
        catch (TypeLoadException)
        {
            // Fall through to constructor declaring type.
        }

        try
        {
            return string.Equals(data.Constructor.DeclaringType?.Name, attributeTypeName, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
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