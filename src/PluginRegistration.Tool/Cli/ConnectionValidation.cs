using System;
using System.Collections.Generic;

namespace PluginRegistration.Tool.Cli;

internal static class ConnectionValidation
{
    public static bool TryValidate(string? connection, out string errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(connection))
        {
            errorMessage = string.Empty;
            return true;
        }

        var missing = GetMissingEnvironmentVariables();
        if (missing.Count == 0)
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage =
            "Dataverse connection is required. Provide --connection / -c or set environment variables for one of the supported auth methods:\n" +
            "  • Access token (DATAVERSE_ACCESS_TOKEN) — recommended with Workload Identity Federation\n" +
            "  • Certificate (AZURE_CLIENT_CERTIFICATE_PATH + ClientId + TenantId)\n" +
            "  • Client secret (DATAVERSE_CLIENT_SECRET / AZURE_CLIENT_SECRET + ClientId + TenantId)\n\n" +
            "Missing: " + string.Join(", ", missing) + ".";

        return false;
    }

    private static List<string> GetMissingEnvironmentVariables()
    {
        var missing = new List<string>();

        var hasUrl = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DATAVERSE_URL"))
                     || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POWERPLATFORM_ENVIRONMENT_URL"));

        if (!hasUrl)
        {
            missing.Add("DATAVERSE_URL (or POWERPLATFORM_ENVIRONMENT_URL)");
        }

        // Check for any complete authentication method supported
        bool hasAccessToken = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DATAVERSE_ACCESS_TOKEN"))
                           || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POWERPLATFORM_ACCESS_TOKEN"));

        bool hasClientId = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DATAVERSE_CLIENT_ID"))
                        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POWERPLATFORM_CLIENT_ID"))
                        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_CLIENT_ID"));

        bool hasClientSecret = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DATAVERSE_CLIENT_SECRET"))
                            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POWERPLATFORM_CLIENT_SECRET"))
                            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET"));

        bool hasCertificate = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DATAVERSE_CLIENT_CERTIFICATE_PATH"))
                           || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POWERPLATFORM_CLIENT_CERTIFICATE_PATH"))
                           || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_CLIENT_CERTIFICATE_PATH"));

        bool hasTenantId = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DATAVERSE_TENANT_ID"))
                        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POWERPLATFORM_TENANT_ID"))
                        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_TENANT_ID"));

        if (hasAccessToken)
        {
            // Access token is sufficient (great for WIF)
            return missing; // URL already checked above
        }

        if (hasCertificate)
        {
            // Certificate auth needs ClientId + TenantId (password optional)
            if (!hasClientId)
                missing.Add("ClientId (DATAVERSE_CLIENT_ID / AZURE_CLIENT_ID) for certificate auth");
            if (!hasTenantId)
                missing.Add("TenantId (DATAVERSE_TENANT_ID / AZURE_TENANT_ID) for certificate auth");
            return missing;
        }

        // Default: secret-based auth (classic or from Azure DevOps service connection)
        if (!hasClientId)
            missing.Add("DATAVERSE_CLIENT_ID (or POWERPLATFORM_CLIENT_ID or AZURE_CLIENT_ID)");

        if (!hasClientSecret)
            missing.Add("DATAVERSE_CLIENT_SECRET (or POWERPLATFORM_CLIENT_SECRET or AZURE_CLIENT_SECRET)");

        if (!hasTenantId)
            missing.Add("DATAVERSE_TENANT_ID (or POWERPLATFORM_TENANT_ID or AZURE_TENANT_ID)");

        return missing;
    }
}