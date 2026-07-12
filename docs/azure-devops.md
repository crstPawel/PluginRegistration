# Azure DevOps

Template: `templates/azure-pipelines-plugin-deploy.yml`

## Service Connections (recommended)

Use an **Azure Resource Manager** service connection instead of storing secrets in Variable Groups.

Benefits:
- Credentials managed centrally in Azure DevOps
- Easy per-environment connections
- Workload Identity Federation support

**Setup:**
1. Project Settings → Service connections → create **Azure Resource Manager** (e.g. `dataverse-dev-spn`)
2. Use `AzureCLI@2` with `azureSubscription`
3. Task sets `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`, `AZURE_TENANT_ID`
4. `pluginreg` picks them up automatically

### Client Secret or Certificate

```yaml
- task: AzureCLI@2
  displayName: Register plugins (DEV)
  inputs:
    azureSubscription: 'dataverse-dev-spn'
    scriptType: bash
    scriptLocation: inlineScript
    inlineScript: |
      cd $(Pipeline.Workspace)/plugins
      pluginreg whoami
      pluginreg deploy --path . --profile dev
  env:
    DATAVERSE_URL: $(DATAVERSE_URL)
```

Uses `AZURE_CLIENT_SECRET` or `AZURE_CLIENT_CERTIFICATE_PATH` depending on the connection.

### Workload Identity Federation (most secure)

```yaml
- task: AzureCLI@2
  displayName: Register plugins (WIF)
  inputs:
    azureSubscription: 'dataverse-dev-wif'
    scriptType: bash
    scriptLocation: inlineScript
    inlineScript: |
      export DATAVERSE_ACCESS_TOKEN=$(az account get-access-token \
        --resource "$DATAVERSE_URL" --query accessToken -o tsv)
      cd $(Pipeline.Workspace)/plugins
      pluginreg whoami
      pluginreg deploy --path . --profile dev
  env:
    DATAVERSE_URL: $(DATAVERSE_URL)
```

## Alternative — Variable Groups

```yaml
- script: |
    cd $(Pipeline.Workspace)/plugins
    pluginreg deploy --path . --profile dev
  displayName: Register plugins
  env:
    DATAVERSE_URL: $(DATAVERSE_URL)
    DATAVERSE_CLIENT_ID: $(DATAVERSE_CLIENT_ID)
    DATAVERSE_CLIENT_SECRET: $(DATAVERSE_CLIENT_SECRET)
    DATAVERSE_TENANT_ID: $(DATAVERSE_TENANT_ID)
```

## General setup

1. Variable groups per environment (at least `DATAVERSE_URL`)
2. Service connections (recommended)
3. Environments with approval gates
4. Publish tool to Azure Artifacts or install from NuGet

Full template: `templates/azure-pipelines-plugin-deploy.yml`.

Authentication details: [authentication.md](authentication.md).