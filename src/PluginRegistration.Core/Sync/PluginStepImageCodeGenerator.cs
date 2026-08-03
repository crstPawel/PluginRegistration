using PluginRegistration.Core.Registration;

namespace PluginRegistration.Core.Sync;

public static class PluginStepImageCodeGenerator
{
    public static string Generate(PluginStepImageModel image, string linePrefix)
    {
        var extras = string.Empty;
        if (!string.IsNullOrWhiteSpace(image.Message))
        {
            extras += $", Message = \"{Escape(image.Message)}\"";
        }

        return
            $"{linePrefix}[PluginStepImage(\"{Escape(image.Name)}\", ImageTypeEnum.{image.ImageType}, {FilteringAttributesParser.FormatForCode(image.Attributes ?? [])}{extras})]";
    }

    private static string Escape(string value) => value.Replace("\"", "\"\"");
}
