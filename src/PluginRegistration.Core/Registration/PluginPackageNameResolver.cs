namespace PluginRegistration.Core.Registration;

/// <summary>
/// Builds Dataverse export-key names that must start with a publisher customization prefix
/// (plugin package, Custom API, Custom API parameters).
/// </summary>
/// <remarks>
/// Dataverse requires certain <c>uniquename</c> values to start with a valid publisher
/// customization prefix (e.g. <c>new_Sample.Plugins</c>, <c>contoso_ProcessAccount</c>).
/// The prefix should come from the solution's publisher in <c>pluginregistration.json</c>,
/// not from hard-coded defaults.
/// </remarks>
public static class PluginPackageNameResolver
{
    /// <summary>
    /// Returns <c>{prefix}_{name}</c> when a publisher prefix is provided and the
    /// name is not already prefixed; otherwise returns <paramref name="packageId"/>.
    /// </summary>
    public static string ResolveRegistrationName(string packageId, string? publisherPrefix)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentException("Package id is required.", nameof(packageId));
        }

        if (string.IsNullOrWhiteSpace(publisherPrefix))
        {
            return packageId;
        }

        string prefix = publisherPrefix.Trim().TrimEnd('_');
        if (prefix.Length == 0)
        {
            return packageId;
        }

        string withUnderscore = prefix + "_";
        if (packageId.StartsWith(withUnderscore, StringComparison.OrdinalIgnoreCase))
        {
            return packageId;
        }

        return withUnderscore + packageId;
    }
}
