# PluginRegistration

Attributes and tooling for registering Dataverse (Dynamics 365) plugins and Custom APIs from source-code attributes.

Full documentation: [README.md](README.md) and [docs/](docs/).

## Packages

| Package | Purpose |
|---------|---------|
| `PluginRegistration.Attributes` | Attributes to decorate plugin classes |
| `PluginRegistration.Tool` | CLI tool (`pluginreg`) for deploy/sync/init |
| `PluginRegistration.Core` | Core library (rarely used directly) |

## Installation

```bash
dotnet add package PluginRegistration.Attributes
dotnet tool install --global PluginRegistration.Tool
```

See [docs/installation.md](docs/installation.md) for build-from-source and private feeds.

## Basic usage

### Plugin step

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

### Custom API

```csharp
[CustomApiRegistration("sample_ProcessAccount", FriendlyName = "Process Account")]
[CustomApiRequestParameter("AccountId", CustomApiParameterTypeEnum.String, IsRequired = true)]
[CustomApiResponseProperty("Success", CustomApiParameterTypeEnum.Boolean)]
public class ProcessAccountCustomApiPlugin : IPlugin
{
    public void Execute(IServiceProvider serviceProvider) { }
}
```

### Deploy

```bash
pluginreg init --path . --profiles dev,test,prod
pluginreg deploy --path . --profile dev
```

More: [docs/getting-started.md](docs/getting-started.md), [docs/plugin-steps.md](docs/plugin-steps.md), [docs/custom-api.md](docs/custom-api.md).