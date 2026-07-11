using Newtonsoft.Json;
using PluginRegistration.Core.Config;
using Xunit;

namespace PluginRegistration.Core.Tests;

public sealed class ConfigScaffoldServiceTests
{
    private static readonly string SamplePluginsPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "Sample.Plugins"));

    [Fact]
    public void Generate_FromSamplePlugins_DiscoversStepsAndCustomApis()
    {
        using var tempDirectory = new TempDirectory();
        CopySamplePluginSources(tempDirectory.Path);

        var outputPath = GenerateConfig(tempDirectory.Path);

        var config = JsonConvert.DeserializeObject<PluginRegistrationConfig>(File.ReadAllText(outputPath))
            ?? throw new InvalidOperationException("Failed to deserialize generated config.");

        var devProfile = config.Profiles["dev"];

        Assert.Equal(
            new[]
            {
                "Sample.Plugins.AccountCreatePlugin.PreOperation",
                "Sample.Plugins.AccountLifecyclePlugin.PostOperation",
                "Sample.Plugins.AccountLifecyclePlugin.PreOperation"
            },
            devProfile.StepOverrides.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray());

        foreach (var stepName in devProfile.StepOverrides.Keys)
        {
            Assert.Equal(string.Empty, devProfile.StepOverrides[stepName].UnSecureConfiguration);
        }

        Assert.Equal(3, devProfile.CustomApis.Count);

        var processAccount = devProfile.CustomApis.Single(api =>
            string.Equals(api.UniqueName, "sample_ProcessAccount", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Process Account", processAccount.DisplayName);
        Assert.Equal("Sample.Plugins.ProcessAccountCustomApiPlugin", processAccount.PluginTypeName);
        Assert.True(processAccount.CreateIfMissing);

        var validateAccount = devProfile.CustomApis.Single(api =>
            string.Equals(api.UniqueName, "sample_ValidateAccount", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Validate Account", validateAccount.DisplayName);
        Assert.Equal("Sample.Plugins.MultiCustomApiPlugin", validateAccount.PluginTypeName);

        var enrichAccount = devProfile.CustomApis.Single(api =>
            string.Equals(api.UniqueName, "sample_EnrichAccount", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Enrich Account", enrichAccount.DisplayName);
        Assert.Equal("Sample.Plugins.MultiCustomApiPlugin", enrichAccount.PluginTypeName);

        Assert.All(config.Profiles["test"].CustomApis, api => Assert.False(api.CreateIfMissing));
        Assert.All(config.Profiles["prod"].CustomApis, api => Assert.False(api.CreateIfMissing));
    }

    [Fact]
    public void Generate_FromSamplePlugins_WritesExpectedDeployEntry()
    {
        using var tempDirectory = new TempDirectory();
        CopySamplePluginSources(tempDirectory.Path);

        var outputPath = GenerateConfig(tempDirectory.Path);
        var config = JsonConvert.DeserializeObject<PluginRegistrationConfig>(File.ReadAllText(outputPath))
            ?? throw new InvalidOperationException("Failed to deserialize generated config.");

        Assert.Single(config.Plugins);
        Assert.Equal("dev,test,prod", config.Plugins[0].Profile);
        Assert.Equal("bin/Release", config.Plugins[0].AssemblyPath);
        Assert.Equal("SampleSolution", config.Plugins[0].Solution);
    }

    private static string GenerateConfig(string workingDirectory)
    {
        var trace = new TestTrace();
        var service = new ConfigScaffoldService(trace);

        return service.Generate(
            workingDirectory,
            ["dev", "test", "prod"],
            assemblyPath: "bin/Release",
            solution: "SampleSolution");
    }

    private static void CopySamplePluginSources(string destinationDirectory)
    {
        foreach (var sourceFile in Directory.EnumerateFiles(SamplePluginsPath, "*.cs", SearchOption.AllDirectories))
        {
            if (sourceFile.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || sourceFile.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(SamplePluginsPath, sourceFile);
            var targetPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(sourceFile, targetPath, overwrite: true);
        }
    }

    private sealed class TestTrace : ITrace
    {
        public void WriteLine(string format, params object?[] args)
        {
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = Directory.CreateTempSubdirectory("pluginreg-init-tests-").FullName;
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