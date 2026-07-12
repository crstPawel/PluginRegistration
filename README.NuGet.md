# PluginRegistration

Attributes and tooling for registering Dataverse (Dynamics 365) plugins and Custom APIs using source-code attributes.

## Packages

| Package                              | Purpose                                      |
|--------------------------------------|----------------------------------------------|
| `PluginRegistration.Attributes`      | Attributes to decorate plugin classes        |
| `PluginRegistration.Tool`            | CLI tool (`pluginreg`) for deploy/sync/init  |
| `PluginRegistration.Core`            | Core library (rarely used directly)          |

## Installation

```bash
# Attributes (add to your plugin project)
dotnet add package PluginRegistration.Attributes

# CLI Tool (global)
dotnet tool install --global PluginRegistration.Tool
```

## Basic Usage

### 1. Decorate your plugin

```csharp
using Microsoft.Xrm.Sdk;
using PluginRegistration.Attributes;

[CrmPluginRegistration(
    "Create", 
    "account", 
    StageEnum.PreOperation, 
    ExecutionModeEnum.Synchronous, 
    "", 
    1, 
    IsolationModeEnum.Sandbox,
    "Account Create Plugin")]
public class AccountCreatePlugin : IPlugin
{
    public void Execute(IServiceProvider serviceProvider)
    {
        // plugin logic
    }
}
```

### 2. Generate configuration and deploy

```bash
# Create pluginregistration.json
pluginreg init --path . --profiles dev,test,prod

# Deploy to Dataverse
pluginreg deploy --path . --profile dev
```

For Custom APIs, step images, and advanced configuration see the full documentation in the repository.