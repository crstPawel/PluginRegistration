using System.IO;

namespace PluginRegistration.Tool.Cli;

internal static class PathValidation
{
    public static DirectoryInfo Resolve(DirectoryInfo? path)
        => path ?? new DirectoryInfo(Directory.GetCurrentDirectory());

    public static bool TryValidateDirectory(DirectoryInfo? path, out string errorMessage)
    {
        var directory = Resolve(path);
        if (directory.Exists)
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = $"Directory does not exist: {directory.FullName}";
        return false;
    }

    public static bool TryValidateConfigFile(DirectoryInfo? path, out string errorMessage)
    {
        if (!TryValidateDirectory(path, out errorMessage))
        {
            return false;
        }

        var configPath = Path.Combine(Resolve(path).FullName, "pluginregistration.json");
        if (File.Exists(configPath))
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage =
            $"Configuration file not found: {configPath}. Run 'pluginreg init --path {Resolve(path).FullName}' first.";

        return false;
    }
}