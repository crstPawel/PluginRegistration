using System.Diagnostics;
using PluginRegistration.Core.Registration;
using Xunit;

namespace PluginRegistration.Core.Tests;

/// <summary>
/// Verifies that plugin class discovery via reflection (used during <c>pluginreg deploy</c>)
/// correctly identifies classes implementing IPlugin, including those inheriting from a base class.
/// </summary>
public sealed class ReflectionHelperTests
{
    private static readonly string SamplePluginsDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "Sample.Plugins"));

    private static readonly string SamplePluginsDll = Path.Combine(
        SamplePluginsDir, "bin", "Debug", "net462", "Sample.Plugins.dll");

    [Fact]
    public void GetPluginTypes_FindsAllPluginsInSampleAssembly()
    {
        EnsureSamplePluginsBuilt();

        Assert.True(File.Exists(SamplePluginsDll), $"Expected sample plugin assembly at {SamplePluginsDll}");

        var directory = Path.GetDirectoryName(SamplePluginsDll)!;
        using var context = ReflectionHelper.CreateLoadContext(directory);
        var assembly = ReflectionHelper.LoadAssembly(context, SamplePluginsDll);

        Assert.NotNull(assembly);

        var pluginTypes = ReflectionHelper.GetPluginTypes(assembly).OrderBy(t => t.FullName).ToList();

        // We expect exactly the four plugin/custom-api classes (none of the base)
        var expected = new[]
        {
            "Sample.Plugins.AccountCreatePlugin",
            "Sample.Plugins.AccountLifecycleCustomApi",
            "Sample.Plugins.AccountLifecyclePlugin",
            "Sample.Plugins.MultiCustomApiPlugin",
            "Sample.Plugins.ProcessAccountCustomApiPlugin"
        };

        Assert.Equal(expected, pluginTypes.Select(t => t.FullName).ToArray());

        // Ensure abstract base class itself is never reported as a plugin implementation
        Assert.DoesNotContain(pluginTypes, t => t.IsAbstract);
        Assert.DoesNotContain(pluginTypes, t => t.Name == "PluginBase");
    }

    [Fact]
    public void GetPluginTypes_ExcludesAbstractClassesAndNonPlugins()
    {
        EnsureSamplePluginsBuilt();

        var directory = Path.GetDirectoryName(SamplePluginsDll)!;
        using var context = ReflectionHelper.CreateLoadContext(directory);
        var assembly = ReflectionHelper.LoadAssembly(context, SamplePluginsDll)!;

        var allTypes = assembly.GetTypes();
        var pluginTypes = ReflectionHelper.GetPluginTypes(assembly).ToHashSet();

        foreach (var type in allTypes)
        {
            if (type.IsAbstract || type.IsInterface)
            {
                Assert.DoesNotContain(type, pluginTypes);
            }
        }
    }

    [Fact]
    public void CreateLoadContext_DoesNotThrowForNetFrameworkPluginAssembly()
    {
        EnsureSamplePluginsBuilt();

        var directory = Path.GetDirectoryName(SamplePluginsDll)!;

        // The main verification for the original error: this must succeed for net462 plugins
        // when the tool itself runs on modern .NET (net10+).
        using var context = ReflectionHelper.CreateLoadContext(directory);
        var assembly = ReflectionHelper.LoadAssembly(context, SamplePluginsDll);

        Assert.NotNull(assembly);
        Assert.NotEmpty(ReflectionHelper.GetPluginTypes(assembly));
    }

    private static void EnsureSamplePluginsBuilt()
    {
        if (File.Exists(SamplePluginsDll))
        {
            // If the dll exists and is reasonably fresh, still ensure it is up to date for changed base class.
            // For simplicity we always (re)build in test.
        }

        var projectFile = Path.Combine(SamplePluginsDir, "Sample.Plugins.csproj");
        Assert.True(File.Exists(projectFile), $"Sample project not found: {projectFile}");

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{projectFile}\" -c Debug --no-restore --nologo --verbosity quiet",
            WorkingDirectory = SamplePluginsDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(120_000);

        if (process.ExitCode != 0)
        {
            throw new Xunit.Sdk.XunitException(
                $"Failed to build sample plugins for test.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        }

        // Give filesystem a moment if needed
        Thread.Sleep(200);
    }

    [Fact]
    public void GetPluginTypes_WorksEvenWhenMicrosoftXrmSdkIsMissingFromPluginDirectory()
    {
        EnsureSamplePluginsBuilt();

        // This simulates the very common real-world situation:
        // - Plugin DLL + our Attributes DLL are present.
        // - Microsoft.Xrm.Sdk.dll (and friends) are NOT next to the plugin (PrivateAssets, NuGet packaging, publish, etc.).
        // The tool must still discover plugin classes (primarily via our registration attributes).
        using var temp = new TempDirectory();
        var isolatedDir = temp.Path;

        // Copy only what a minimal package usually ships
        var pluginDll = Path.Combine(SamplePluginsDir, "bin", "Debug", "net462", "Sample.Plugins.dll");
        var attrDll = Path.Combine(SamplePluginsDir, "bin", "Debug", "net462", "PluginRegistration.Attributes.dll");

        File.Copy(pluginDll, Path.Combine(isolatedDir, "Sample.Plugins.dll"), overwrite: true);
        if (File.Exists(attrDll))
            File.Copy(attrDll, Path.Combine(isolatedDir, "PluginRegistration.Attributes.dll"), overwrite: true);

        // Make sure Microsoft.Xrm.Sdk.* is NOT in the isolated directory
        Assert.False(Directory.GetFiles(isolatedDir, "Microsoft.Xrm*.dll").Any(), "Test setup failed: Xrm.Sdk should be absent");

        using var context = ReflectionHelper.CreateLoadContext(isolatedDir);
        var assembly = ReflectionHelper.LoadAssembly(context, Path.Combine(isolatedDir, "Sample.Plugins.dll"));

        Assert.NotNull(assembly);

        var pluginTypes = ReflectionHelper.GetPluginTypes(assembly).Select(t => t.FullName).OrderBy(n => n).ToList();

        // We should still find the plugins thanks to:
        // 1. NuGet cache probing for Microsoft.Xrm.Sdk.dll (so interface resolution can succeed), OR
        // 2. The attribute-based fallback inside GetPluginTypes (if interface resolution still fails).
        Assert.NotEmpty(pluginTypes);

        // At least some of our known plugins must be present (they carry [PluginRegistration] / [CustomApiRegistration])
        Assert.Contains("Sample.Plugins.AccountCreatePlugin", pluginTypes);
        Assert.Contains("Sample.Plugins.MultiCustomApiPlugin", pluginTypes);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pluginreg-missing-sdk-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch { /* best effort */ }
        }
    }
}
