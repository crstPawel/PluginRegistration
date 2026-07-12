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
        return image.ImageType switch
        {
            ImageTypeEnum.PreImage => step.Stage is StageEnum.PreValidation or StageEnum.PreOperation,
            ImageTypeEnum.PostImage => step.Stage == StageEnum.PostOperation,
            ImageTypeEnum.Both => true,
            _ => false
        };
    }

    private static ParsedStepImage Parse(CustomAttributeData data)
    {
        var arguments = data.ConstructorArguments.ToArray();

        return new ParsedStepImage
        {
            Name = (string)arguments[0].Value!,
            ImageType = (ImageTypeEnum)Enum.ToObject(typeof(ImageTypeEnum), (int)arguments[1].Value!),
            Attributes = (string)arguments[2].Value!
        };
    }

    private sealed class ParsedStepImage
    {
        public string Name { get; init; } = string.Empty;
        public ImageTypeEnum ImageType { get; init; }
        public string Attributes { get; init; } = string.Empty;
    }
}