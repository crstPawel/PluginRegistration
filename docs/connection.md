# Connecting to Dataverse

The tool connects via `Microsoft.PowerPlatform.Dataverse.Client` (Service Principal / Client Secret by default).

## Data you need

| Value | Where to find it |
|-------|------------------|
| **Environment URL** | Power Platform Admin Center → Environments → open environment → URL, e.g. `https://org-dev.crm4.dynamics.com` |
| **Tenant ID** | Azure Portal → Microsoft Entra ID → Overview → Tenant ID |
| **Client ID** | Azure Portal → App registrations → Your app → Application (client) ID |
| **Client Secret** | App registration → Certificates & secrets → New client secret |

## Register the application in Entra ID (one-time)

1. Azure Portal → **Microsoft Entra ID** → **App registrations** → **New registration**
2. Name e.g. `pluginreg-local`, account type: **Single tenant**
3. Copy **Application (client) ID** and **Directory (tenant) ID**
4. **Certificates & secrets** → **New client secret** → copy the value (shown once)
5. Power Platform Admin Center → environment → **Settings** → **Users + permissions** → **Application users**:
   - Add application user (your Client ID)
   - Assign a security role, e.g. **System Administrator** (DEV)

## Environment variables (recommended locally)

**Linux / macOS:**

```bash
export DATAVERSE_URL="https://your-org.crm4.dynamics.com"
export DATAVERSE_CLIENT_ID="00000000-0000-0000-0000-000000000000"
export DATAVERSE_CLIENT_SECRET="your-secret"
export DATAVERSE_TENANT_ID="00000000-0000-0000-0000-000000000000"
```

**Windows PowerShell:**

```powershell
$env:DATAVERSE_URL="https://your-org.crm4.dynamics.com"
$env:DATAVERSE_CLIENT_ID="00000000-0000-0000-0000-000000000000"
$env:DATAVERSE_CLIENT_SECRET="your-secret"
$env:DATAVERSE_TENANT_ID="00000000-0000-0000-0000-000000000000"
```

You can use a `.env` file loaded before running — do not commit it to git.

## Verify the connection

```bash
cd /path/to/PluginRegistrationTool
dotnet run --project src/PluginRegistration.Tool -- whoami
```

Expected output:

```
OrganizationId: ...
BusinessUnitId: ...
UserId: ...
```

## Connection string alternative

```bash
dotnet run --project src/PluginRegistration.Tool -- whoami \
  --connection "AuthType=ClientSecret;Url=https://your-org.crm4.dynamics.com;ClientId=<id>;ClientSecret=<secret>;TenantId=<tenant>"
```

Other authentication methods (certificate, access token) are described in [authentication.md](authentication.md).