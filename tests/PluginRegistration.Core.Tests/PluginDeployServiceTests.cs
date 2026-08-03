using PluginRegistration.Core.Deploy;
using Xunit;

namespace PluginRegistration.Core.Tests;

public sealed class PluginDeployServiceTests
{
    [Fact]
    public void ResolvePackagePaths_FindsNupkgUnderFolderPattern()
    {
        using var temp = new TempDirectory();
        var releaseDir = Path.Combine(temp.Path, "bin", "Release");
        Directory.CreateDirectory(releaseDir);
        File.WriteAllText(Path.Combine(releaseDir, "Sample.Plugins.1.0.0.nupkg"), "dummy");
        File.WriteAllText(Path.Combine(releaseDir, "Sample.Plugins.1.0.0.snupkg"), "symbols");

        var paths = PluginDeployService.ResolvePackagePaths(temp.Path, "bin/Release").ToList();

        Assert.Single(paths);
        Assert.EndsWith("Sample.Plugins.1.0.0.nupkg", paths[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolvePackagePaths_MissingDirectory_Throws()
    {
        using var temp = new TempDirectory();

        Assert.Throws<PluginRegistrationException>(() =>
            PluginDeployService.ResolvePackagePaths(temp.Path, "bin/Release").ToList());
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = Directory.CreateTempSubdirectory("pluginreg-deploy-tests-").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
