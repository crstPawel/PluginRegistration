# Getting started

## 1. Add attributes to plugin classes

```bash
dotnet add package PluginRegistration.Attributes
```

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
public class AccountCreatePlugin : IPlugin
{
    public void Execute(IServiceProvider serviceProvider) { }
}
```

Custom API uses a **separate** attribute — see [custom-api.md](custom-api.md).

## 2. Generate `pluginregistration.json`

```bash
pluginreg init --path . --solution MySolution
```

| Option | Default | Description |
|--------|---------|-------------|
| `--assembly-path` | `bin/Release` | Path to DLL output |
| `--solution` | — | Dataverse solution name |
| `--force` | — | Overwrite existing file |

Or copy and edit `samples/Sample.Plugins/pluginregistration.json`.

Example configuration with environment-specific values via pipeline variables:

```json
{
  "plugins": [
    {
      "assemblyPath": "bin/Release",
      "solution": "MySolution"
    }
  ],
  "stepOverrides": {
    "MyPlugin.AccountCreate.PreOperation": {
      "unSecureConfiguration": "${API_URL}"
    }
  }
}
```

Environment selection happens at the pipeline level — each stage sets `DATAVERSE_*` connection variables and runs `pluginreg deploy`. Use `${VARIABLE_NAME}` placeholders in `stepOverrides` for values that differ per environment.

Details: [configuration.md](configuration.md). Internals: [init.md](init.md).

## 3. Build and deploy

```bash
dotnet build -c Release

export DATAVERSE_URL="https://org.crm4.dynamics.com"
export DATAVERSE_CLIENT_ID="<app-id>"
export DATAVERSE_CLIENT_SECRET="<secret>"
export DATAVERSE_TENANT_ID="<tenant-id>"

pluginreg deploy --path .
```

Internals: [deploy.md](deploy.md).

---

## Tool and plugins in separate folders

Typical layout:

```
/home/user/PluginRegistrationTool/     ← this repo (the tool)
/home/user/MyCrmPlugins/               ← your plugin project
    src/MyCompany.Plugins/
```

Prerequisites for `sync`:

1. Plugin classes in `.cs` files
2. Plugins **already registered** in Dataverse (assembly + plugintype + steps)
3. Full type names in code must match `plugintype.typename`

`sync` does not require `pluginregistration.json`.

```bash
export DATAVERSE_URL="https://contoso-dev.crm4.dynamics.com"
# ... other DATAVERSE_* vars

dotnet run --project /home/user/PluginRegistrationTool/src/PluginRegistration.Tool -- \
  sync --path /home/user/MyCrmPlugins/src/MyCompany.Plugins
```

See [sync.md](sync.md) for full behavior.

---

## Full workflow: sync → init → deploy

```bash
# A. Pull steps from DEV into code
pluginreg sync --path /path/to/MyPluginProject/src/MyPlugins

# B. Generate pluginregistration.json
pluginreg init --path /path/to/MyPluginProject

# C. Build
cd /path/to/MyPluginProject && dotnet build -c Release

# D. Deploy to DEV
pluginreg deploy --path /path/to/MyPluginProject
```

---

## Typical team workflow

```
┌─────────────────┐     ┌──────────────┐     ┌─────────────────┐
│  Code + attrs   │────▶│ dotnet build │────▶│ pluginreg deploy│
│ [PluginReg...]  │     │   Release    │     │                 │
└─────────────────┘     └──────────────┘     └─────────────────┘
         ▲                                              │
         │                                              ▼
         │                                    Dataverse (target env)
         │
         │     pluginreg sync (optional — after manual
         └──── changes in Plugin Registration Tool)
```

1. Developer adds `[PluginRegistration]` and Custom API attributes
2. Environment-specific values use `${VARIABLE_NAME}` in `stepOverrides` — set in pipeline per stage
3. Pipeline builds DLL, sets `DATAVERSE_*` for the target environment, and runs `pluginreg deploy`
4. After manual PRT changes — `pluginreg sync` updates attributes in the repo

---

## Sample in this repository

[`samples/Sample.Plugins/`](../samples/Sample.Plugins/):

- `AccountCreatePlugin.cs` — plugin step + Custom API
- `pluginregistration.json` — deploy configuration

```bash
cd samples/Sample.Plugins
dotnet build -c Release
pluginreg deploy --path .
```