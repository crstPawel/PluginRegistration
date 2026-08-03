using System.Diagnostics;
using PluginRegistration.Attributes;
using PluginRegistration.Core.Registration;
using PluginRegistration.Core.Sync;
using Xunit;

namespace PluginRegistration.Core.Tests;

/// <summary>
/// Filtering attributes are read from plugin assemblies via MetadataLoadContext.
/// Runtime typeof(string) identity checks fail there and used to drop string[] args.
/// </summary>
public sealed class FilteringAttributesParserTests
{
    private static readonly string SamplePluginsDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "Sample.Plugins"));

    private static readonly string SamplePluginsDll = Path.Combine(
        SamplePluginsDir, "bin", "Debug", "net462", "Sample.Plugins.dll");

    [Fact]
    public void Parse_FromSampleAssembly_ReadsFilteringAttributesViaMetadataLoadContext()
    {
        EnsureSamplePluginsBuilt();

        var directory = Path.GetDirectoryName(SamplePluginsDll)!;
        using var context = ReflectionHelper.CreateLoadContext(directory);
        var assembly = ReflectionHelper.LoadAssembly(context, SamplePluginsDll)!;
        var type = ReflectionHelper.GetPluginTypes(assembly)
            .First(t => t.Name == "AccountLifecyclePlugin");

        var steps = ReflectionHelper.GetRegistrationAttributes(type)
            .Select(AttributeParser.Parse)
            .ToList();

        Assert.NotEmpty(steps);
        Assert.All(steps, step =>
        {
            Assert.NotEmpty(step.FilteringAttributes);
            Assert.Contains("name", step.FilteringAttributes);
        });
    }

    [Fact]
    public void Parse_FromSampleAssembly_ReadsPluginStepImageAttributes()
    {
        EnsureSamplePluginsBuilt();

        var directory = Path.GetDirectoryName(SamplePluginsDll)!;
        using var context = ReflectionHelper.CreateLoadContext(directory);
        var assembly = ReflectionHelper.LoadAssembly(context, SamplePluginsDll)!;
        var type = ReflectionHelper.GetPluginTypes(assembly)
            .First(t => t.Name == "AccountLifecyclePlugin");

        var postStep = ReflectionHelper.GetRegistrationAttributes(type)
            .Select(AttributeParser.Parse)
            .First(step => step.Stage == StageEnum.PostOperation);

        var images = PluginStepImageReader.GetImages(type, postStep);

        var image = Assert.Single(images);
        Assert.Equal("PostImage", image.Name);
        Assert.Contains("name", image.Attributes);
        Assert.Contains("telephone1", image.Attributes);
    }

    [Fact]
    public void Generate_EmitsCollectionExpressionForMultipleFilteringAttributes()
    {
        var attribute = new PluginRegistrationAttribute(
            MessageTypeEnum.Update,
            "account",
            StageEnum.PreOperation,
            ExecutionModeEnum.Synchronous,
            ["name", "accountnumber"],
            1);

        var code = AttributeCodeGenerator.Generate(attribute, indentation: "\n    ");

        Assert.Equal(
            "\n    [PluginRegistration(MessageTypeEnum.Update, \"account\", StageEnum.PreOperation, ExecutionModeEnum.Synchronous, new string[] { \"name\", \"accountnumber\" }, 1)]",
            code);
        Assert.DoesNotContain("[\"name\"", code);
    }

    private static void EnsureSamplePluginsBuilt()
    {
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
    }
}
