using System.Reflection;
using System.Runtime.Loader;

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

    private static readonly string[] RegistrationAttributeNames =
    [
        "PluginRegistrationAttribute",
        "CrmPluginRegistrationAttribute",
        "CustomApiRegistration",
        "CustomApiRegistrationAttribute",
        "CrmCustomApiRegistration",
        "CrmCustomApiRegistrationAttribute"
    ];

    public static bool ShouldIgnoreAssembly(string fileName) => IgnoredAssemblies.Contains(fileName);

    public static MetadataLoadContext CreateLoadContext(string assemblyDirectory)
    {
        // PathAssemblyResolver requires concrete assembly file paths (not directories).
        // On modern .NET the core assembly is System.Private.CoreLib; include the whole
        // runtime directory so MetadataLoadContext can resolve System.Runtime / netstandard.
        var resolverPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        foreach (var dll in Directory.EnumerateFiles(runtimeDir, "*.dll"))
        {
            resolverPaths.Add(dll);
        }

        var coreLibLocation = typeof(object).Assembly.Location;
        if (!string.IsNullOrWhiteSpace(coreLibLocation))
        {
            resolverPaths.Add(coreLibLocation);
        }

        if (Directory.Exists(assemblyDirectory))
        {
            foreach (var dll in Directory.EnumerateFiles(assemblyDirectory, "*.dll"))
            {
                resolverPaths.Add(dll);
            }
        }

        // When Xrm assemblies are PrivateAssets / not copied next to the plugin, probe the NuGet cache.
        foreach (var dll in ProbeNuGetPackageDlls("microsoft.crmsdk.coreassemblies", "Microsoft.Xrm.Sdk.dll", "Microsoft.Crm.Sdk.Proxy.dll"))
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
        return GetLoadableTypes(assembly)
            .Where(IsConcretePluginType);
    }

    public static IEnumerable<Type> GetWorkflowActivityTypes(Assembly assembly)
    {
        return GetLoadableTypes(assembly)
            .Where(type =>
            {
                try
                {
                    return InheritsFromCodeActivity(type);
                }
                catch (FileNotFoundException)
                {
                    return false;
                }
                catch (TypeLoadException)
                {
                    return false;
                }
            });
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null)!;
        }
    }

    private static bool IsConcretePluginType(Type type)
    {
        try
        {
            if (!type.IsClass || type.IsAbstract)
            {
                return false;
            }

            if (type.GetInterfaces().Any(i => string.Equals(i.Name, "IPlugin", StringComparison.Ordinal)))
            {
                return true;
            }
        }
        catch (FileNotFoundException)
        {
            // Microsoft.Xrm.Sdk (or another dependency) missing — fall through to attributes.
        }
        catch (TypeLoadException)
        {
            // Base type / interface could not be loaded.
        }

        // Attribute-based fallback: types decorated with registration attributes are plugins
        // even when IPlugin cannot be resolved without the CRM SDK assembly.
        return HasRegistrationAttributes(type);
    }

    private static bool HasRegistrationAttributes(Type type)
    {
        try
        {
            foreach (var data in type.GetCustomAttributesData())
            {
                if (IsRegistrationAttribute(data))
                {
                    return true;
                }
            }
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (TypeLoadException)
        {
            return false;
        }

        return false;
    }

    private static bool IsRegistrationAttribute(CustomAttributeData data)
    {
        try
        {
            if (RegistrationAttributeNames.Contains(data.AttributeType.Name, StringComparer.Ordinal))
            {
                return true;
            }
        }
        catch (FileNotFoundException)
        {
            // Fall through to constructor declaring type.
        }
        catch (TypeLoadException)
        {
            // Fall through to constructor declaring type.
        }

        try
        {
            var declaringName = data.Constructor.DeclaringType?.Name;
            return declaringName is not null
                   && RegistrationAttributeNames.Contains(declaringName, StringComparer.Ordinal);
        }
        catch
        {
            return false;
        }
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

    public static IEnumerable<CustomAttributeData> GetRegistrationAttributes(Type type)
    {
        var attributes = type.GetCustomAttributesData()
            .Where(a => a.AttributeType.Name is "PluginRegistrationAttribute" or "CrmPluginRegistrationAttribute")
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

    public static IEnumerable<CustomAttributeData> GetCustomApiRegistrationAttributes(Type type)
    {
        return type.GetCustomAttributesData()
            .Where(a => a.AttributeType.Name is "CustomApiRegistration" or "CustomApiRegistrationAttribute"
                or "CrmCustomApiRegistration" or "CrmCustomApiRegistrationAttribute");
    }

    private static IEnumerable<string> ProbeNuGetPackageDlls(string packageId, params string[] fileNames)
    {
        var packagesRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget",
            "packages",
            packageId);

        if (!Directory.Exists(packagesRoot))
        {
            yield break;
        }

        // Prefer the newest package version that has the requested assemblies.
        foreach (var versionDir in Directory.EnumerateDirectories(packagesRoot)
                     .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var libDir = Path.Combine(versionDir, "lib");
            if (!Directory.Exists(libDir))
            {
                continue;
            }

            var foundAny = false;
            foreach (var fileName in fileNames)
            {
                var match = Directory.EnumerateFiles(libDir, fileName, SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (match is not null)
                {
                    foundAny = true;
                    yield return match;
                }
            }

            if (foundAny)
            {
                yield break;
            }
        }
    }
}
