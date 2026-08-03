using PluginRegistration.Core.Registration;
using Xunit;

namespace PluginRegistration.Core.Tests;

public sealed class PluginPackageNameResolverTests
{
    [Theory]
    [InlineData("Sample.Plugins", "contoso", "contoso_Sample.Plugins")]
    [InlineData("Sample.Plugins", "new", "new_Sample.Plugins")]
    [InlineData("MyPlugin", "ava", "ava_MyPlugin")]
    public void ResolveRegistrationName_PrefixesWithPublisher(string packageId, string prefix, string expected)
    {
        Assert.Equal(expected, PluginPackageNameResolver.ResolveRegistrationName(packageId, prefix));
    }

    [Theory]
    [InlineData("contoso_Sample.Plugins", "contoso", "contoso_Sample.Plugins")]
    [InlineData("CONTOSO_Sample.Plugins", "contoso", "CONTOSO_Sample.Plugins")]
    [InlineData("Contoso_MyPlugin", "Contoso", "Contoso_MyPlugin")]
    public void ResolveRegistrationName_DoesNotDoublePrefix(string packageId, string prefix, string expected)
    {
        Assert.Equal(expected, PluginPackageNameResolver.ResolveRegistrationName(packageId, prefix));
    }

    [Theory]
    [InlineData("Sample.Plugins", null)]
    [InlineData("Sample.Plugins", "")]
    [InlineData("Sample.Plugins", "   ")]
    [InlineData("Sample.Plugins", "_")]
    public void ResolveRegistrationName_WithoutPrefix_ReturnsPackageId(string packageId, string? prefix)
    {
        Assert.Equal(packageId, PluginPackageNameResolver.ResolveRegistrationName(packageId, prefix));
    }

    [Fact]
    public void ResolveRegistrationName_TrimsTrailingUnderscoreOnPrefix()
    {
        Assert.Equal(
            "contoso_Sample.Plugins",
            PluginPackageNameResolver.ResolveRegistrationName("Sample.Plugins", "contoso_"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveRegistrationName_EmptyPackageId_Throws(string? packageId)
    {
        Assert.Throws<ArgumentException>(() =>
            PluginPackageNameResolver.ResolveRegistrationName(packageId!, "contoso"));
    }
}
