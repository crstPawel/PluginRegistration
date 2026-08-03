using PluginRegistration.Attributes;
using PluginRegistration.Core.Registration;
using PluginRegistration.Core.Sync;
using Xunit;

namespace PluginRegistration.Core.Tests;

public sealed class AttributeCodeGeneratorTests
{
    [Fact]
    public void Generate_FormatsPluginRegistrationAttribute_OnSingleLine()
    {
        var attribute = new PluginRegistrationAttribute(
            MessageTypeEnum.Create,
            "account",
            StageEnum.PostOperation,
            ExecutionModeEnum.Synchronous,
            ["name", "accountnumber"],
            executionOrder: 10);

        attribute.Id = "11111111-1111-1111-1111-111111111111";

        var code = AttributeCodeGenerator.Generate(
            attribute,
            indentation: "\r\n    ",
            pluginTypeName: "Sample.Plugins.AccountCreatePlugin");

        Assert.Equal(
            "\r\n    [PluginRegistration(MessageTypeEnum.Create, \"account\", StageEnum.PostOperation, ExecutionModeEnum.Synchronous, new string[] { \"name\", \"accountnumber\" }, 10, Id = \"11111111-1111-1111-1111-111111111111\")]",
            code);
        Assert.DoesNotContain("\r\n    MessageTypeEnum", code);
        Assert.Equal(1, code.Count(c => c == '\n'));
    }

    [Fact]
    public void Generate_OmitsDefaultStepName_WhenItMatchesResolver()
    {
        var attribute = new PluginRegistrationAttribute(
            MessageTypeEnum.Update,
            "contact",
            StageEnum.PreOperation,
            ExecutionModeEnum.Synchronous,
            [],
            executionOrder: 1);

        // Default name for type + stage — should not be written as a named argument.
        attribute.Name = "Sample.Plugins.ContactUpdatePlugin.PreOperation";

        var code = AttributeCodeGenerator.Generate(
            attribute,
            indentation: "\n    ",
            pluginTypeName: "Sample.Plugins.ContactUpdatePlugin");

        Assert.Equal(
            "\n    [PluginRegistration(MessageTypeEnum.Update, \"contact\", StageEnum.PreOperation, ExecutionModeEnum.Synchronous, new string[0], 1)]",
            code);
        Assert.DoesNotContain("Name =", code);
    }

    [Fact]
    public void Generate_IncludesCustomName_WhenDifferentFromDefault()
    {
        var attribute = new PluginRegistrationAttribute(
            MessageTypeEnum.Delete,
            "account",
            StageEnum.PreValidation,
            ExecutionModeEnum.Asynchronous,
            ["statecode"],
            executionOrder: 5);

        attribute.Name = "Custom Delete Step";
        attribute.DeleteAsyncOperation = true;

        var code = AttributeCodeGenerator.Generate(
            attribute,
            indentation: "\n    ",
            pluginTypeName: "Sample.Plugins.AccountDeletePlugin");

        Assert.Equal(
            "\n    [PluginRegistration(MessageTypeEnum.Delete, \"account\", StageEnum.PreValidation, ExecutionModeEnum.Asynchronous, new string[] { \"statecode\" }, 5, Name = \"Custom Delete Step\", DeleteAsyncOperation = true)]",
            code);
    }

    [Fact]
    public void Generate_PluginStepImage_OnSingleLine()
    {
        var code = PluginStepImageCodeGenerator.Generate(
            new PluginStepImageModel
            {
                Name = "PostImage",
                ImageType = ImageTypeEnum.PostImage,
                Attributes = ["name", "telephone1"]
            },
            linePrefix: "\n    ");

        Assert.Equal(
            "\n    [PluginStepImage(\"PostImage\", ImageTypeEnum.PostImage, new string[] { \"name\", \"telephone1\" })]",
            code);
    }
}
