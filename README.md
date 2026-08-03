# PluginRegistration Tool

Register Dataverse (Dynamics 365) plugins and Custom APIs from C# source code — using attributes on plugin classes and a CLI tool for deploy and sync.

Designed for **local development** and **Azure DevOps pipelines**: pack your plugin as a NuGet package, decorate classes with attributes, and run `pluginreg deploy` to upload the `.nupkg` and create or update registration metadata in Dataverse.

## What it does

| Capability | Description |
|------------|-------------|
| **Deploy** | Upload plugin NuGet packages (`.nupkg`) and register plugin steps, step images, and Custom APIs |
| **Sync** | Pull registration metadata from Dataverse into `[PluginRegistration]` and related attributes in `.cs` files |

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
| Plugin step attributes | [docs/plugin-steps.md](docs/plugin-steps.md) |
| Custom API attributes | [docs/custom-api.md](docs/custom-api.md) |
| Deploy (internals) | [docs/deploy.md](docs/deploy.md) |
| Sync (internals) | [docs/sync.md](docs/sync.md) |
| NuGet publishing | [docs/nuget.md](docs/nuget.md) |
| Azure DevOps | [docs/azure-devops.md](docs/azure-devops.md) |
| Troubleshooting | [docs/troubleshooting.md](docs/troubleshooting.md) |

## Sample project

See [`samples/Sample.Plugins/`](samples/Sample.Plugins/) for plugin steps and Custom API attributes.

## Quick deploy

```bash
dotnet pack -c Release
pluginreg deploy --path samples/Sample.Plugins --package-path bin/Release --solution SampleSolution
```

## Requirements

- .NET SDK **10.0** (see `global.json`)
- Access to a Dataverse environment
- Packed plugin `.nupkg` for `deploy` (not required for `sync`)
