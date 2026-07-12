# Installation

## Requirements

- .NET SDK **10.0** (see `global.json` in the repository root)
- Access to a Dataverse environment (Service Principal or connection string)
- Built plugin assembly (`.dll`) — required only for `deploy`, not for `sync`

The tool and plugin projects **can live in separate directories**. For `sync`, point to plugin source code with `--path`.

---

## Build from source (this repository)

```bash
cd /path/to/PluginRegistrationTool
dotnet build -c Release
```

### Run without a global install

```bash
# Help
dotnet run --project src/PluginRegistration.Tool -- --help

# Test connection
dotnet run --project src/PluginRegistration.Tool -- whoami

# Sync a separate plugin project
dotnet run --project src/PluginRegistration.Tool -- sync --path /path/to/MyPluginProject/src/MyPlugins
```

> After changing the tool code, rebuild (`dotnet build`) or reinstall the global tool package.

---

## Install globally as `pluginreg`

From a local pack:

```bash
dotnet pack -c Release -o nupkg
dotnet tool install --global PluginRegistration.Tool --version 1.0.3 --add-source ./nupkg
```

From NuGet.org or a private feed:

```bash
dotnet tool install --global PluginRegistration.Tool --version 1.0.3
# private feed:
dotnet tool install --global PluginRegistration.Tool --version 1.0.3 --add-source <feed-url>
```

Then from any directory:

```bash
pluginreg sync --path /path/to/MyPluginProject/src/MyPlugins
pluginreg deploy --path . --profile dev
```

---

## Add attributes to a plugin project

```bash
dotnet add package PluginRegistration.Attributes
```

Or in `.csproj`:

```xml
<PackageReference Include="PluginRegistration.Attributes" Version="1.0.3" />
```

See [README.NuGet.md](../README.NuGet.md) for minimal attribute examples.

For package publishing and feed configuration, see [nuget.md](nuget.md).