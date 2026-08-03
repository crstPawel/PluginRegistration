# Getting started

## 1. Add attributes to plugin projects

Add the `PluginRegistration.Attributes` package and decorate plugin classes with registration attributes.

```csharp
using Microsoft.Xrm.Sdk;
using PluginRegistration.Attributes;

[PluginRegistration(
    MessageTypeEnum.Create,
    "account",
    StageEnum.PostOperation,
    ExecutionModeEnum.Synchronous,
    ["name"],
    1)]
public sealed class AccountCreatePlugin : IPlugin
{
    public void Execute(IServiceProvider serviceProvider) { }
}
```

Details: [plugin-steps.md](plugin-steps.md), [custom-api.md](custom-api.md).

## 2. Pack and deploy

```bash
dotnet pack -c Release

export DATAVERSE_URL="https://org.crm4.dynamics.com"
export DATAVERSE_CLIENT_ID="<app-id>"
export DATAVERSE_CLIENT_SECRET="<secret>"
export DATAVERSE_TENANT_ID="<tenant-id>"

pluginreg deploy \
  --path . \
  --package-path bin/Release \
  --solution MySolution
```

Environment selection happens at the pipeline level — each stage sets `DATAVERSE_*` and runs `pluginreg deploy` with the target `--solution` if needed.

Internals: [deploy.md](deploy.md). CLI: [cli.md](cli.md).

---

## 3. Optional: sync attributes from Dataverse

```bash
pluginreg sync --path .
```

`sync` does not require a config file. It overwrites registration attributes from the current environment.

---

## Full workflow: sync → pack → deploy

```bash
# A. Optional: pull current registration from Dataverse into code
pluginreg sync --path /path/to/MyPluginProject

# B. Pack
dotnet pack -c Release

# C. Deploy
pluginreg deploy \
  --path /path/to/MyPluginProject \
  --package-path bin/Release \
  --solution MySolution
```

---

## Checklist

1. Plugin classes implement `IPlugin` and carry attributes
2. `dotnet pack` produces `*.nupkg` under `--package-path`
3. Connection via `DATAVERSE_*` or `--connection`
4. `--solution` set when you want solution components and a stable publisher prefix

See also: [authentication.md](authentication.md), [troubleshooting.md](troubleshooting.md).
