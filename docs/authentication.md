# Authentication

## Environment variables (recommended in Azure DevOps)

The tool supports **three authentication methods**:

| Method | Required variables | Best for |
|--------|-------------------|----------|
| **Client Secret** | `DATAVERSE_URL` + ClientId + ClientSecret + TenantId | Simple scenarios |
| **Certificate** | `DATAVERSE_URL` + `..._CLIENT_CERTIFICATE_PATH` + ClientId + TenantId | Better security |
| **Access Token** | `DATAVERSE_URL` + `DATAVERSE_ACCESS_TOKEN` | **Workload Identity Federation (WIF)** |

### Variable prefixes (priority order)

- `DATAVERSE_*`
- `POWERPLATFORM_*`
- `AZURE_*` (ClientId/Secret/Tenant/CertificatePath — from service connection)

### Azure DevOps Service Connection variables

- `AZURE_CLIENT_ID`
- `AZURE_CLIENT_SECRET`
- `AZURE_TENANT_ID`
- `AZURE_CLIENT_CERTIFICATE_PATH`
- `AZURE_CLIENT_CERTIFICATE_PASSWORD` (optional)

### Workload Identity Federation

```bash
export DATAVERSE_ACCESS_TOKEN=$(az account get-access-token --resource "$DATAVERSE_URL" --query accessToken -o tsv)
```

**Best practice (2025+):** prefer WIF (no long-lived secrets) and obtain the token in the pipeline.

See [azure-devops.md](azure-devops.md) for pipeline examples.

## Connection string

```
AuthType=ClientSecret;Url=https://org.crm4.dynamics.com;ClientId=<id>;ClientSecret=<secret>;TenantId=<tenant>
```

Pass via `--connection` / `-c` on any command, or set up env vars as in [connection.md](connection.md).