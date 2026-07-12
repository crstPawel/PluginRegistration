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
            .Where(data => data.AttributeType.Name == nameof(PluginStepImageAttribute))
            .Select(Parse)
            .Where(image => MatchesStep(image, step))
            .Select(image => new PluginStepImageModel
            {
                Name = image.Name,
                ImageType = image.ImageType,
                Attributes = image.Attributes
            })
            .ToList();
    }

    private static bool MatchesStep(ParsedStepImage image, PluginRegistrationAttribute step)
    {
        if (image.Stage != step.Stage)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(image.Message))
        {
            return true;
        }

        return string.Equals(image.Message, step.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ParsedStepImage Parse(CustomAttributeData data)
    {
        var arguments = data.ConstructorArguments.ToArray();
        var image = new ParsedStepImage
        {
            Stage = (StageEnum)Enum.ToObject(typeof(StageEnum), (int)arguments[0].Value!),
            Name = (string)arguments[1].Value!,
            ImageType = (ImageTypeEnum)Enum.ToObject(typeof(ImageTypeEnum), (int)arguments[2].Value!),
            Attributes = (string?)arguments[3].Value
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

    private sealed class ParsedStepImage
    {
        public StageEnum Stage { get; init; }
        public string Name { get; init; } = string.Empty;
        public ImageTypeEnum ImageType { get; init; }
        public string? Attributes { get; init; }
        public string? Message { get; set; }
    }
}