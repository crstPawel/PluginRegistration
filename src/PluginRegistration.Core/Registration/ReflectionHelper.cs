using System.Reflection;
using System.Runtime.Loader;
using PluginRegistration.Attributes;

namespace PluginRegistration.Core.Registration;

public static class ReflectionHelper
{
    private static readonly HashSet<string> IgnoredAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.Crm.Sdk.Proxy.dll",
        "Microsoft.IdentityModel.dll",
        "Microsoft.Xrm.Sdk.dll",
        "Microsoft.Xrm.Sdk.Workflow.dll",
        "Microsoft.IdentityModel.Clients.ActiveDirectory.dll",
        "Microsoft.Extensions.FileSystemGlobbing.dll",
        "Microsoft.Xrm.Sdk.Deployment.dll",
        "Microsoft.Xrm.Tooling.Connector.dll",
        "Newtonsoft.Json.dll",
        "PluginRegistration.Attributes.dll",
        "PluginRegistration.Core.dll",
        "System.Net.Http.dll",
        "Microsoft.Rest.ClientRuntime.dll"
    };

    public static bool ShouldIgnoreAssembly(string fileName) => IgnoredAssemblies.Contains(fileName);

    public static MetadataLoadContext CreateLoadContext(string assemblyDirectory)
    {
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var resolverPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            runtimeDir,
            assemblyDirectory
        };

        foreach (var dll in Directory.EnumerateFiles(assemblyDirectory, "*.dll"))
        {
            resolverPaths.Add(dll);
        }

        return new MetadataLoadContext(new PathAssemblyResolver(resolverPaths));
    }

    public static Assembly LoadAssembly(MetadataLoadContext context, string path)
    {
        try
        {
            return context.LoadFromAssemblyPath(path);
        }
        catch (FileLoadException)
        {
            return null!;
        }
    }

    public static IEnumerable<Type> GetPluginTypes(Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(type => type.IsClass
                && !type.IsAbstract
                && type.GetInterfaces().Any(i => i.Name == "IPlugin"));
    }

    public static IEnumerable<Type> GetWorkflowActivityTypes(Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(type => InheritsFromCodeActivity(type));
    }

    private static bool InheritsFromCodeActivity(Type type)
    {
        var current = type;
        while (current is not null && current.Name != "Object")
        {
            if (current.Name == "CodeActivity")
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    public static IEnumerable<CustomAttributeData> GetCustomApiRegistrationAttributes(Type type)
    {
        return type.GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType.Name == nameof(CustomApiRegistration));
    }

    public static IEnumerable<CustomAttributeData> GetRegistrationAttributes(Type type)
    {
        var attributes = type.GetCustomAttributesData()
            .Where(a => a.AttributeType.Name == nameof(PluginRegistrationAttribute))
            .ToList();

        var duplicateNames = attributes
            .Select(AttributeParser.Parse)
            .Where(a => a.Stage is not null)
            .Select(a => PluginStepNameResolver.ApplyStepName(type, a))
            .GroupBy(a => a.Name!, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.Skip(1))
            .ToList();

        if (duplicateNames.Count > 0)
        {
            var names = string.Join(", ", duplicateNames.Select(a => a.Name));
            throw new PluginRegistrationException($"Duplicate plugin step names found: {names}");
        }

        return attributes;
    }
}