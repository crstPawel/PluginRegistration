using PluginRegistration.Core.Registration;

namespace PluginRegistration.Core.Sync;

public static class PluginStepImageCodeGenerator
{
    public static string Generate(PluginStepImageModel image, string linePrefix)
    {
        return
            $"{linePrefix}[PluginStepImage(\"{Escape(image.Name)}\", ImageTypeEnum.{image.ImageType}, {FilteringAttributesParser.FormatForCode(image.Attributes ?? [])})]";
    }

    private static string Escape(string value) => value.Replace("\"", "\"\"");
}
