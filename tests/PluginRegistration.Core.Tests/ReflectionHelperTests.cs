using PluginRegistration.Core.Registration;
using Xunit;

namespace PluginRegistration.Core.Tests;

/// <summary>
/// Verifies that plugin class discovery via reflection (used during <c>pluginreg deploy</c>)
/// correctly identifies classes implementing IPlugin, including those inheriting from a base class.
/// </summary>
public sealed class ReflectionHelperTests
{
    [Fact]
    public void GetPluginTypes_FindsAllPluginsInSampleAssembly()
    {
        SamplePluginsTestHost.EnsureBuilt();

        var dll = SamplePluginsTestHost.AssemblyPath;
        Assert.True(File.Exists(dll), $"Expected sample plugin assembly at {dll}");

        var directory = Path.GetDirectoryName(dll)!;
        using var context = ReflectionHelper.CreateLoadContext(directory);
        var assembly = ReflectionHelper.LoadAssembly(context, dll);

        Assert.NotNull(assembly);

        var pluginTypes = ReflectionHelper.GetPluginTypes(assembly).OrderBy(t => t.FullName).ToList();

        // Concrete IPlugin types in samples (not PluginBase / helpers)
        var expected = new[]
        {
            "Sample.Plugins.AccountCreatePlugin",
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
        SamplePluginsTestHost.EnsureBuilt();

        var dll = SamplePluginsTestHost.AssemblyPath;
        var directory = Path.GetDirectoryName(dll)!;
        using var context = ReflectionHelper.CreateLoadContext(directory);
        var assembly = ReflectionHelper.LoadAssembly(context, dll)!;

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
        SamplePluginsTestHost.EnsureBuilt();

        var dll = SamplePluginsTestHost.AssemblyPath;
        var directory = Path.GetDirectoryName(dll)!;

        // The main verification for the original error: this must succeed for net462 plugins
        // when the tool itself runs on modern .NET (net10+).
        using var context = ReflectionHelper.CreateLoadContext(directory);
        var assembly = ReflectionHelper.LoadAssembly(context, dll);

        Assert.NotNull(assembly);
        Assert.NotEmpty(ReflectionHelper.GetPluginTypes(assembly));
    }

    [Fact]
    public void GetPluginTypes_WorksEvenWhenMicrosoftXrmSdkIsMissingFromPluginDirectory()
    {
        SamplePluginsTestHost.EnsureBuilt();

        // This simulates the very common real-world situation:
        // - Plugin DLL + our Attributes DLL are present.
        // - Microsoft.Xrm.Sdk.dll (and friends) are NOT next to the plugin (PrivateAssets, NuGet packaging, publish, etc.).
        // The tool must still discover plugin classes (primarily via our registration attributes).
        using var temp = new TempDirectory();
        var isolatedDir = temp.Path;

        // Copy only what a minimal package usually ships
        var pluginDll = SamplePluginsTestHost.AssemblyPath;
        var attrDll = Path.Combine(
            SamplePluginsTestHost.ProjectDirectory, "bin", "Debug", "net462", "PluginRegistration.Attributes.dll");

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
