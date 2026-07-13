using System.Reflection;
using System.Runtime.Loader;
using PluginRegistration.Attributes;
using System.Linq;

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

        // Include all assemblies from the current .NET runtime directory.
        // This allows MetadataLoadContext to resolve core assemblies (mscorlib/System.Private.CoreLib)
        // and System.* dependencies when inspecting plugin assemblies compiled for .NET Framework (net462/net472).
        foreach (var dll in Directory.EnumerateFiles(runtimeDir, "*.dll"))
        {
            resolverPaths.Add(dll);
        }

        // Add every DLL we can find next to / under the target assembly (highest priority for dependencies).
        foreach (var dll in Directory.EnumerateFiles(assemblyDirectory, "*.dll", SearchOption.AllDirectories))
        {
            resolverPaths.Add(dll);
        }

        // Aggressively search a few levels up (handles cases where DLL is in net462/ subfolder
        // but some dependencies are in a sibling folder or the project bin root).
        AddDllsFromParentDirectories(assemblyDirectory, resolverPaths, maxLevels: 5);

        // Try to locate Dataverse / Power Platform SDK assemblies from the NuGet cache as a last resort.
        // IMPORTANT: we add them *after* local directories and we de-duplicate known SDK files
        // to avoid "assembly has already been loaded into this MetadataLoadContext".
        AddDataverseSdkAssembliesFromNuGetCache(resolverPaths);

        // De-duplicate known platform SDK assemblies by filename (keep only one path per well-known name).
        // Different physical files declaring the same assembly identity (same version/PKT) cause MLC to throw.
        DeduplicateKnownSdkAssemblies(resolverPaths);

        return new MetadataLoadContext(new PathAssemblyResolver(resolverPaths));
    }

    private static void AddDllsFromParentDirectories(string startDirectory, HashSet<string> paths, int maxLevels)
    {
        try
        {
            var current = new DirectoryInfo(startDirectory);
            for (int level = 0; level < maxLevels && current != null; level++)
            {
                // Add DLLs directly in this folder (not recursive here to avoid huge scans)
                foreach (var dll in current.EnumerateFiles("*.dll", SearchOption.TopDirectoryOnly))
                {
                    paths.Add(dll.FullName);
                }

                // Also pick up any Microsoft.Xrm.* or Microsoft.Crm.* we see while climbing
                foreach (var dll in current.EnumerateFiles("Microsoft.Xrm*.dll", SearchOption.TopDirectoryOnly))
                    paths.Add(dll.FullName);
                foreach (var dll in current.EnumerateFiles("Microsoft.Crm*.dll", SearchOption.TopDirectoryOnly))
                    paths.Add(dll.FullName);

                current = current.Parent;
            }
        }
        catch
        {
            // Best effort only.
        }
    }

    private static void AddDataverseSdkAssembliesFromNuGetCache(HashSet<string> paths)
    {
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var packagesRoot = Path.Combine(userProfile, ".nuget", "packages");
            if (!Directory.Exists(packagesRoot))
                return;

            // Common packages that contain the core Dataverse SDK assemblies
            string[] packageIdHints =
            {
                "microsoft.crmsdk.coreassemblies",
                "microsoft.powerplatform.dataverse.client",
                "microsoft.xrm.sdk",
                "microsoft.crmsdk.coreassemblies.9"
            };

            foreach (var hint in packageIdHints)
            {
                var packageRoot = Path.Combine(packagesRoot, hint);
                if (!Directory.Exists(packageRoot))
                    continue;

                foreach (var versionDir in Directory.EnumerateDirectories(packageRoot))
                {
                    // Common layout: lib/net462/Microsoft.Xrm.Sdk.dll etc.
                    var libRoot = Path.Combine(versionDir, "lib");
                    if (Directory.Exists(libRoot))
                    {
                        foreach (var dll in Directory.EnumerateFiles(libRoot, "Microsoft.Xrm.Sdk.dll", SearchOption.AllDirectories))
                            paths.Add(dll);
                        foreach (var dll in Directory.EnumerateFiles(libRoot, "Microsoft.Crm.Sdk.Proxy.dll", SearchOption.AllDirectories))
                            paths.Add(dll);
                        foreach (var dll in Directory.EnumerateFiles(libRoot, "Microsoft.Xrm.Sdk.Workflow.dll", SearchOption.AllDirectories))
                            paths.Add(dll);
                    }

                    // Fallback: any matching file under the version folder
                    foreach (var dll in Directory.EnumerateFiles(versionDir, "Microsoft.Xrm.Sdk.dll", SearchOption.AllDirectories))
                        paths.Add(dll);
                    foreach (var dll in Directory.EnumerateFiles(versionDir, "Microsoft.Crm.Sdk.Proxy.dll", SearchOption.AllDirectories))
                        paths.Add(dll);
                }
            }
        }
        catch
        {
            // Never let NuGet cache probing break context creation.
        }
    }

    /// <summary>
    /// Ensures that for well-known Dataverse platform assemblies we keep only a single path in the resolver.
    /// Loading two different files that both claim to be "Microsoft.Xrm.Sdk, Version=9.0.0.0, ..." into the
    /// same MetadataLoadContext produces "The assembly '...' has already been loaded".
    /// We prefer the first path that was added (local plugin dir > parents > NuGet).
    /// </summary>
    private static void DeduplicateKnownSdkAssemblies(HashSet<string> paths)
    {
        string[] knownSdkSimpleNames = { "Microsoft.Xrm.Sdk.dll", "Microsoft.Crm.Sdk.Proxy.dll", "Microsoft.Xrm.Sdk.Workflow.dll" };

        foreach (var simpleName in knownSdkSimpleNames)
        {
            var matches = paths
                .Where(p => string.Equals(Path.GetFileName(p), simpleName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count <= 1)
                continue;

            // Keep only the first one (order of insertion roughly gives priority to local dirs).
            // Remove the rest.
            foreach (var extra in matches.Skip(1))
            {
                paths.Remove(extra);
            }
        }
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
        catch (FileNotFoundException)
        {
            // Can happen if the main assembly itself has hard-to-resolve dependencies at load time.
            return null!;
        }
    }

    public static IEnumerable<Type> GetPluginTypes(Assembly assembly)
    {
        // Use a resilient enumerator because GetTypes() + GetInterfaces() can throw
        // FileNotFoundException for Microsoft.Xrm.Sdk (and similar platform assemblies)
        // when those DLLs are not co-located with the plugin (very common with packaging,
        // PrivateAssets, publish outputs, and NuGet plugin packages).
        foreach (var type in GetLoadableTypes(assembly))
        {
            if (!type.IsClass || type.IsAbstract)
                continue;

            if (SafeImplementsIPlugin(type))
            {
                yield return type;
                continue;
            }

            // Very important fallback for the "SDK assembly missing" scenario:
            // If the type is decorated with our registration attributes, we still want to
            // treat it as a plugin type even if we couldn't resolve the IPlugin interface.
            // (Attributes are stored in the assembly's own metadata and usually don't
            // require the full Dataverse SDK to be present.)
            if (HasRegistrationAttributes(type))
            {
                yield return type;
            }
        }
    }

    public static IEnumerable<Type> GetWorkflowActivityTypes(Assembly assembly)
    {
        foreach (var type in GetLoadableTypes(assembly))
        {
            if (type.IsClass && !type.IsAbstract && SafeInheritsFromCodeActivity(type))
                yield return type;
        }
    }

    /// <summary>
    /// Safely enumerate types from a MetadataLoadContext-loaded assembly.
    /// Some referenced assemblies (Microsoft.Xrm.Sdk etc.) may be missing from disk.
    /// </summary>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        Type[]? types = null;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Partial success — some types could be loaded.
            types = ex.Types?.Where(t => t != null).Cast<Type>().ToArray();
        }
        catch (FileNotFoundException)
        {
            // The entire type graph had an unresolvable reference (e.g. Microsoft.Xrm.Sdk).
            // Try a lighter surface.
            try
            {
                types = assembly.DefinedTypes.Cast<Type>().ToArray();
            }
            catch
            {
                types = Array.Empty<Type>();
            }
        }
        catch (Exception)
        {
            types = Array.Empty<Type>();
        }

        return types ?? Array.Empty<Type>();
    }

    private static bool SafeImplementsIPlugin(Type type)
    {
        try
        {
            return type.GetInterfaces().Any(i => i.Name == "IPlugin");
        }
        catch (FileNotFoundException ex) when (IsMissingDataversePlatformAssembly(ex))
        {
            // Interface could not be resolved. The fallback in GetPluginTypes (attribute check) will catch real plugins.
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool SafeInheritsFromCodeActivity(Type type)
    {
        try
        {
            var current = type;
            while (current is not null && current.Name != "Object")
            {
                if (current.Name == "CodeActivity")
                    return true;

                current = current.BaseType;
            }
            return false;
        }
        catch (FileNotFoundException ex) when (IsMissingDataversePlatformAssembly(ex))
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsMissingDataversePlatformAssembly(FileNotFoundException ex)
    {
        var msg = ex.Message;
        return msg.Contains("Microsoft.Xrm.Sdk", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Microsoft.Crm.Sdk", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Microsoft.Xrm.Sdk.Workflow", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasRegistrationAttributes(Type type)
    {
        try
        {
            // These methods internally use GetCustomAttributesData which is usually safe
            // because the attributes live in the plugin's own code or PluginRegistration.Attributes.dll.
            return GetRegistrationAttributes(type).Any() || GetCustomApiRegistrationAttributes(type).Any();
        }
        catch
        {
            return false;
        }
    }

    private static bool InheritsFromCodeActivity(Type type)
    {
        // Legacy direct version kept for any internal direct callers; prefer SafeInheritsFromCodeActivity.
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
        try
        {
            return type.GetCustomAttributesData()
                .Where(attribute => attribute.AttributeType.Name == nameof(CustomApiRegistration));
        }
        catch (FileNotFoundException)
        {
            // Attribute type resolution failed (rare for our own attributes).
            return Enumerable.Empty<CustomAttributeData>();
        }
    }

    public static IEnumerable<CustomAttributeData> GetRegistrationAttributes(Type type)
    {
        List<CustomAttributeData> attributes;
        try
        {
            attributes = type.GetCustomAttributesData()
                .Where(a => a.AttributeType.Name == nameof(PluginRegistrationAttribute))
                .ToList();
        }
        catch (FileNotFoundException)
        {
            return Enumerable.Empty<CustomAttributeData>();
        }

        // Duplicate detection still needs to run on successfully read attributes.
        try
        {
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
        }
        catch (FileNotFoundException)
        {
            // Ignore resolution issues during duplicate checking.
        }

        return attributes;
    }
}