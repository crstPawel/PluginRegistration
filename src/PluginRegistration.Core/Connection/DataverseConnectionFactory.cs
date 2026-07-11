using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;

namespace PluginRegistration.Core.Connection;

public static class DataverseConnectionFactory
{
    public static IOrganizationService Connect(string? connectionString = null)
    {
        ServiceClient.MaxConnectionTimeout = TimeSpan.FromHours(1);

        // 1. Explicit connection string has highest priority
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return ConnectWithConnectionString(connectionString);
        }

        var url = GetFirstEnv("DATAVERSE_URL", "POWERPLATFORM_ENVIRONMENT_URL");

        // 2. Access Token auth — best for Workload Identity Federation (WIF) + AzureCLI@2
        // Obtain token with: az account get-access-token --resource "$DATAVERSE_URL" --query accessToken -o tsv
        var accessToken = GetFirstEnv("DATAVERSE_ACCESS_TOKEN", "POWERPLATFORM_ACCESS_TOKEN");
        if (!string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(url))
        {
            return ConnectWithAccessToken(url, accessToken);
        }

        // 3. Connection-string based auth (ClientSecret or Certificate)
        // Supports Azure DevOps Service Connections via AZURE_* variables
        var connStr = BuildConnectionStringFromEnvironment();
        return ConnectWithConnectionString(connStr);
    }

    private static IOrganizationService ConnectWithConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new PluginRegistrationException("Connection string is required.");
        }

        var client = new ServiceClient(connectionString);

        if (!client.IsReady)
        {
            throw new PluginRegistrationException(
                $"Unable to connect to Dataverse: {client.LastError ?? client.LastException?.Message}");
        }

        return client;
    }

    private static IOrganizationService ConnectWithAccessToken(string url, string accessToken)
    {
        // Token provider: Func<string, Task<string>> according to the library (v1.2.x)
        // The single string parameter is typically the resource/scope.
        var client = new ServiceClient(
            new Uri(url),
            _ => Task.FromResult(accessToken),
            useUniqueInstance: false,
            logger: null!);

        if (!client.IsReady)
        {
            throw new PluginRegistrationException(
                $"Unable to connect to Dataverse using access token: {client.LastError ?? client.LastException?.Message}");
        }

        return client;
    }

    /// <summary>
    /// Builds a connection string from environment variables.
    /// Supports both classic secrets and certificates.
    /// Also supports Azure DevOps Service Connection variables (AZURE_*).
    /// </summary>
    public static string BuildConnectionStringFromEnvironment()
    {
        var url = GetFirstEnv("DATAVERSE_URL", "POWERPLATFORM_ENVIRONMENT_URL");

        var clientId = GetFirstEnv(
            "DATAVERSE_CLIENT_ID", "POWERPLATFORM_CLIENT_ID", "AZURE_CLIENT_ID");

        var tenantId = GetFirstEnv(
            "DATAVERSE_TENANT_ID", "POWERPLATFORM_TENANT_ID", "AZURE_TENANT_ID");

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(tenantId))
        {
            throw new PluginRegistrationException(
                "Missing required environment variables. Need DATAVERSE_URL (or POWERPLATFORM_ENVIRONMENT_URL) + ClientId + TenantId " +
                "(from DATAVERSE_*, POWERPLATFORM_*, or AZURE_* variables).");
        }

        // Certificate-based authentication (preferred over secret when available)
        var certPath = GetFirstEnv(
            "DATAVERSE_CLIENT_CERTIFICATE_PATH", "POWERPLATFORM_CLIENT_CERTIFICATE_PATH", "AZURE_CLIENT_CERTIFICATE_PATH");

        if (!string.IsNullOrWhiteSpace(certPath))
        {
            var certPassword = GetFirstEnv(
                "DATAVERSE_CLIENT_CERTIFICATE_PASSWORD", "POWERPLATFORM_CLIENT_CERTIFICATE_PASSWORD", "AZURE_CLIENT_CERTIFICATE_PASSWORD");

            if (string.IsNullOrWhiteSpace(certPassword))
            {
                // Many certs from Azure DevOps / pipelines have no password or empty
                certPassword = "";
            }

            return $"AuthType=Certificate;Url={url};ClientId={clientId};TenantId={tenantId};CertificateFile={certPath};CertificatePassword={certPassword}";
        }

        // Secret-based (classic or from Azure RM service connection)
        var clientSecret = GetFirstEnv(
            "DATAVERSE_CLIENT_SECRET", "POWERPLATFORM_CLIENT_SECRET", "AZURE_CLIENT_SECRET");

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new PluginRegistrationException(
                "No authentication method found. Provide either:\n" +
                "  • Client secret (DATAVERSE_CLIENT_SECRET / AZURE_CLIENT_SECRET), or\n" +
                "  • Certificate path (AZURE_CLIENT_CERTIFICATE_PATH), or\n" +
                "  • Access token (DATAVERSE_ACCESS_TOKEN) for Workload Identity Federation.");
        }

        return $"AuthType=ClientSecret;Url={url};ClientId={clientId};ClientSecret={clientSecret};TenantId={tenantId}";
    }

    private static string? GetFirstEnv(params string[] names)
    {
        foreach (var name in names)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return null;
    }
}