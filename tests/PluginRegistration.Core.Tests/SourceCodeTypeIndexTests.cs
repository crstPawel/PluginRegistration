using Xunit;

namespace PluginRegistration.Core.Tests;

public sealed class SourceCodeTypeIndexTests
{
    [Fact]
    public void Build_DetectsPluginsInheritingFromCustomBaseThatImplementsIPlugin()
    {
        var root = CreateTempSourceTree(
            ("PluginBase.cs", """
                namespace Sample.Plugins;

                public abstract class PluginBase : IPlugin
                {
                }
                """),
            ("AccountPlugin.cs", """
                namespace Sample.Plugins;

                public class AccountPlugin : PluginBase
                {
                }
                """),
            ("ContactPlugin.cs", """
                namespace Sample.Plugins;

                public sealed class ContactPlugin : PluginBase
                {
                }
                """),
            ("DirectPlugin.cs", """
                namespace Sample.Plugins;

                public class DirectPlugin : IPlugin
                {
                }
                """));

        try
        {
            var index = PluginRegistration.Core.Sync.SourceCodeTypeIndex.Build(root);

            Assert.Equal(3, index.PluginTypeCount);
            Assert.Contains("Sample.Plugins.AccountPlugin", index.GetPluginTypesInFile(Path.Combine(root, "AccountPlugin.cs")));
            Assert.Contains("Sample.Plugins.ContactPlugin", index.GetPluginTypesInFile(Path.Combine(root, "ContactPlugin.cs")));
            Assert.Contains("Sample.Plugins.DirectPlugin", index.GetPluginTypesInFile(Path.Combine(root, "DirectPlugin.cs")));
            Assert.Empty(index.GetPluginTypesInFile(Path.Combine(root, "PluginBase.cs")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Build_DetectsNestedCustomBaseInheritance()
    {
        var root = CreateTempSourceTree(
            ("DomainPluginBase.cs", """
                namespace Acme.Crm;

                public abstract class DomainPluginBase : PluginBase
                {
                }
                """),
            ("LeadPlugin.cs", """
                namespace Acme.Crm;

                public class LeadPlugin : DomainPluginBase
                {
                }
                """));

        try
        {
            var index = PluginRegistration.Core.Sync.SourceCodeTypeIndex.Build(root);

            // DomainPluginBase is abstract → not counted; LeadPlugin inherits PluginBase via DomainPluginBase.
            Assert.Equal(1, index.PluginTypeCount);
            Assert.Contains("Acme.Crm.LeadPlugin", index.GetPluginTypesInFile(Path.Combine(root, "LeadPlugin.cs")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempSourceTree(params (string FileName, string Content)[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), "pluginreg-typeindex-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        foreach (var (fileName, content) in files)
        {
            File.WriteAllText(Path.Combine(root, fileName), content);
        }

        return root;
    }
}
