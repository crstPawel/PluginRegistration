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
}
