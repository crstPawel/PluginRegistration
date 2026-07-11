using PluginRegistration.Attributes;
using PluginRegistration.Core.Registration;

namespace PluginRegistration.Core.Sync;

public static class PluginStepImageCodeGenerator
{
    public static string Generate(
        StageEnum stage,
        string? message,
        PluginStepImageModel image,
        string indentation)
    {
        var extras = string.Empty;

        if (!string.IsNullOrWhiteSpace(message))
        {
            extras += $"{indentation},Message = \"{Escape(message)}\"";
        }

        return string.Format(
            "{0}[CrmPluginStepImage(StageEnum.{1}, \"{2}\", ImageTypeEnum.{3}, \"{4}\"{5}{0})]",
            indentation,
            stage,
            Escape(image.Name),
            image.ImageType,
            Escape(image.Attributes ?? string.Empty),
            extras);
    }

    private static string Escape(string value) => value.Replace("\"", "\"\"");
}