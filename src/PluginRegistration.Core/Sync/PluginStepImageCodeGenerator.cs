using PluginRegistration.Core.Registration;

namespace PluginRegistration.Core.Sync;

public static class PluginStepImageCodeGenerator
{
    public static string Generate(PluginStepImageModel image, string indentation)
    {
        return string.Format(
            "{0}[PluginStepImage(\"{1}\", ImageTypeEnum.{2}, {3}){0}]",
            indentation,
            Escape(image.Name),
            image.ImageType,
            FilteringAttributesParser.FormatForCode(image.Attributes ?? []));
    }

    private static string Escape(string value) => value.Replace("\"", "\"\"");
}
