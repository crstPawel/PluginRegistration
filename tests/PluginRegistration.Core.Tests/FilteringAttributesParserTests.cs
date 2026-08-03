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
    [Fact]
    public void Parse_FromSampleAssembly_ReadsFilteringAttributesViaMetadataLoadContext()
    {
        SamplePluginsTestHost.EnsureBuilt();

        var dll = SamplePluginsTestHost.AssemblyPath;
        var directory = Path.GetDirectoryName(dll)!;
        using var context = ReflectionHelper.CreateLoadContext(directory);
        var assembly = ReflectionHelper.LoadAssembly(context, dll)!;
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
        SamplePluginsTestHost.EnsureBuilt();

        var dll = SamplePluginsTestHost.AssemblyPath;
        var directory = Path.GetDirectoryName(dll)!;
        using var context = ReflectionHelper.CreateLoadContext(directory);
        var assembly = ReflectionHelper.LoadAssembly(context, dll)!;
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
}
