using System.Reflection;
using PluginRegistration.Attributes;

namespace PluginRegistration.Core.Registration;

public static class PluginStepImageReader
{
    public static IReadOnlyList<PluginStepImageModel> GetImages(
        Type pluginType,
        PluginRegistrationAttribute step)
    {
        if (step.Stage is null)
        {
            return [];
        }

        return pluginType.GetCustomAttributesData()
            .Where(data => IsStepImageAttribute(data))
            .Select(Parse)
            .Where(image => MatchesStep(image, step))
            .Select(image => new PluginStepImageModel
            {
                Name = image.Name,
                ImageType = image.ImageType,
                Attributes = image.Attributes,
                Message = image.Message
            })
            .ToList();
    }

    private static bool IsStepImageAttribute(CustomAttributeData data)
    {
        try
        {
            var name = data.AttributeType.Name;
            return name is nameof(PluginStepImageAttribute) or "CrmPluginStepImageAttribute";
        }
        catch
        {
            try
            {
                var name = data.Constructor.DeclaringType?.Name;
                return name is nameof(PluginStepImageAttribute) or "CrmPluginStepImageAttribute";
            }
            catch
            {
                return false;
            }
        }
    }

    private static bool MatchesStep(ParsedStepImage image, PluginRegistrationAttribute step)
    {
        if (!string.IsNullOrWhiteSpace(image.Message)
            && !string.Equals(image.Message, step.Message, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return IsImageTypeCompatibleWithStage(image.ImageType, step.Stage!.Value);
    }

    private static bool IsImageTypeCompatibleWithStage(ImageTypeEnum imageType, StageEnum stage)
    {
        return imageType switch
        {
            ImageTypeEnum.PreImage => stage is StageEnum.PreValidation or StageEnum.PreOperation,
            ImageTypeEnum.PostImage => stage is StageEnum.PostOperation,
            ImageTypeEnum.Both => true,
            _ => false
        };
    }

    private static ParsedStepImage Parse(CustomAttributeData data)
    {
        var arguments = data.ConstructorArguments.ToArray();

        // Current: PluginStepImage(name, imageType, attributes)
        // Legacy: PluginStepImage(stage, name, imageType, attributes)
        string name;
        ImageTypeEnum imageType;
        string[] attributes;

        if (arguments.Length >= 4 && arguments[0].ArgumentType.Name == nameof(StageEnum))
        {
            name = (string)arguments[1].Value!;
            imageType = (ImageTypeEnum)Enum.ToObject(typeof(ImageTypeEnum), (int)arguments[2].Value!);
            attributes = ParseAttributes(arguments[3]);
        }
        else if (arguments.Length >= 3)
        {
            name = (string)arguments[0].Value!;
            imageType = (ImageTypeEnum)Enum.ToObject(typeof(ImageTypeEnum), (int)arguments[1].Value!);
            attributes = ParseAttributes(arguments[2]);
        }
        else
        {
            throw new PluginRegistrationException(
                "Unsupported PluginStepImage attribute constructor.");
        }

        var image = new ParsedStepImage
        {
            Name = name,
            ImageType = imageType,
            Attributes = attributes
        };

        foreach (var namedArgument in data.NamedArguments)
        {
            if (namedArgument.MemberName == nameof(PluginStepImageAttribute.Message))
            {
                image.Message = (string?)namedArgument.TypedValue.Value;
            }
        }

        return image;
    }

    private static string[] ParseAttributes(CustomAttributeTypedArgument argument)
    {
        // string[] (preferred) or legacy comma-separated string
        if (argument.Value is string text)
        {
            return FilteringAttributesParser.SplitCommaSeparated(text);
        }

        return FilteringAttributesParser.ParseArray(argument);
    }

    private sealed class ParsedStepImage
    {
        public string Name { get; init; } = string.Empty;
        public ImageTypeEnum ImageType { get; init; }
        public string[] Attributes { get; init; } = [];
        public string? Message { get; set; }
    }
}
