# PluginRegistration Tool

Register Dataverse (Dynamics 365) plugins and Custom APIs from C# source code — using attributes on plugin classes and a CLI tool for deploy, sync, and configuration scaffolding.

Designed for **local development** and **Azure DevOps pipelines**: build your plugin assembly, decorate classes with attributes, and run `pluginreg deploy` to upload the DLL and create or update registration metadata in Dataverse.

## What it does

| Capability | Description |
|------------|-------------|
| **Deploy** | Upload plugin assemblies and register plugin steps, step images, secure configuration, and Custom APIs |
| **Sync** | Pull registration metadata from Dataverse into `[PluginRegistration]` and related attributes in `.cs` files |
| **Init** | Generate `pluginregistration.json` with environment profiles from existing attributes in code |

Registration is **declarative**: plugin steps use `[PluginRegistration]`, Custom APIs use a separate `[CustomApiRegistration]` attribute, with `[CustomApiRequestParameter]` / `[CustomApiResponseProperty]` and `[PluginStepImage]` for related metadata.

## Packages

| Package | Purpose |
|---------|---------|
| [`PluginRegistration.Attributes`](README.NuGet.md) | Attributes for plugin projects — add this to your plugin `.csproj` |
| `PluginRegistration.Tool` | Global CLI `pluginreg` |
| `PluginRegistration.Core` | Registration library (rarely referenced directly) |

## Documentation

| Topic | Guide |
|-------|-------|
| Installation & local setup | [docs/installation.md](docs/installation.md) |
| Dataverse connection | [docs/connection.md](docs/connection.md) |
| Quick start | [docs/getting-started.md](docs/getting-started.md) |
| CLI reference | [docs/cli.md](docs/cli.md) |
| Authentication | [docs/authentication.md](docs/authentication.md) |
| `pluginregistration.json` | [docs/configuration.md](docs/configuration.md) |
| Plugin step attributes | [docs/plugin-steps.md](docs/plugin-steps.md) |
| Custom API attributes | [docs/custom-api.md](docs/custom-api.md) |
| Deploy (internals) | [docs/deploy.md](docs/deploy.md) |
| Sync (internals) | [docs/sync.md](docs/sync.md) |
| Init (internals) | [docs/init.md](docs/init.md) |
| NuGet publishing | [docs/nuget.md](docs/nuget.md) |
| Azure DevOps | [docs/azure-devops.md](docs/azure-devops.md) |
| Troubleshooting | [docs/troubleshooting.md](docs/troubleshooting.md) |

## Sample project

See [`samples/Sample.Plugins/`](samples/Sample.Plugins/) for plugin steps, Custom API, and a ready-made `pluginregistration.json`.

## Requirements

- .NET SDK **10.0** (see `global.json`)
- Access to a Dataverse environment
- Built plugin `.dll` for `deploy` (not required for `sync`)