using System.IO.Compression;
using System.Xml.Linq;

namespace PluginRegistration.Core.Registration
{
    internal static class NuGetPackageReader
    {
        public static string GetPackageId(string nupkgPath)
        {
            using ZipArchive archive = ZipFile.OpenRead(nupkgPath);

            ZipArchiveEntry? nuspecEntry = archive.Entries.FirstOrDefault(entry =>
                entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)
                && !entry.FullName.Contains('/', StringComparison.Ordinal));

            if (nuspecEntry is null)
            {
                throw new PluginRegistrationException($"No .nuspec file found in package: {nupkgPath}");
            }

            using Stream stream = nuspecEntry.Open();
            XDocument document = XDocument.Load(stream);
            XNamespace ns = document.Root?.GetDefaultNamespace() ?? XNamespace.None;
            string? id = document.Root?.Element(ns + "metadata")?.Element(ns + "id")?.Value;

            if (String.IsNullOrWhiteSpace(id))
            {
                throw new PluginRegistrationException($"No <id> element found in nuspec: {nupkgPath}");
            }

            return id;
        }

        public static string ExtractToTempDirectory(string nupkgPath)
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "pluginreg-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            ZipFile.ExtractToDirectory(nupkgPath, tempDirectory);
            return tempDirectory;
        }

        public static IEnumerable<string> GetPluginAssemblyPaths(string extractedPackageDirectory)
        {
            string libDirectory = Path.Combine(extractedPackageDirectory, "lib");
            if (Directory.Exists(libDirectory))
            {
                return Directory.EnumerateFiles(libDirectory, "*.dll", SearchOption.AllDirectories)
                    .Where(path => !ReflectionHelper.ShouldIgnoreAssembly(Path.GetFileName(path)))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
            }

            return Directory.EnumerateFiles(extractedPackageDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                .Where(path => !ReflectionHelper.ShouldIgnoreAssembly(Path.GetFileName(path)))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        }
    }
}
