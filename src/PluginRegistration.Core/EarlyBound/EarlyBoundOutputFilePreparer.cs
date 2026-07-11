namespace PluginRegistration.Core.EarlyBound;

internal static class EarlyBoundOutputFilePreparer
{
    public static void PrepareForOverwrite(string outputDirectory, ITrace trace)
    {
        if (!Directory.Exists(outputDirectory))
        {
            return;
        }

        var updatedFiles = 0;
        foreach (var file in Directory.EnumerateFiles(outputDirectory, "*.cs", SearchOption.AllDirectories))
        {
            if (!TryClearReadOnly(file))
            {
                continue;
            }

            updatedFiles++;
        }

        if (updatedFiles > 0)
        {
            trace.WriteLine("Cleared read-only flag on {0} existing file(s) before generation.", updatedFiles);
        }
    }

    private static bool TryClearReadOnly(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        var attributes = File.GetAttributes(filePath);
        if (!attributes.HasFlag(FileAttributes.ReadOnly))
        {
            return false;
        }

        File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
        return true;
    }
}