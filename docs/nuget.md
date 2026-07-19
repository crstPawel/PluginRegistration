# NuGet packages and publishing

## Packages

| Package | Description |
|---------|-------------|
| `PluginRegistration.Attributes` | `[PluginRegistration]`, `[CustomApiRegistration]`, `[CustomApiRequestParameter]`, `[CustomApiResponseProperty]`, `[PluginStepImage]` — **add to plugin projects** |
| `PluginRegistration.Core` | Registration library (rarely used directly) |
| `PluginRegistration.Tool` | Global CLI `pluginreg` |

Minimal usage examples: [README.NuGet.md](../README.NuGet.md).

## Install in plugin projects

```bash
dotnet add package PluginRegistration.Attributes
```

```xml
<PackageReference Include="PluginRegistration.Attributes" Version="1.0.3" />
```

## Install the CLI tool

```bash
dotnet tool install --global PluginRegistration.Tool --version 1.0.3
dotnet tool install --global PluginRegistration.Tool --version 1.0.3 --add-source <feed-url>
```

---

## Publishing new versions

Version is in `Directory.Build.props`:

```xml
<Version>1.0.3</Version>
```

Build packages:

```bash
dotnet pack -c Release -o ./artifacts
```

Produces:
- `PluginRegistration.Attributes.*.nupkg` + `.snupkg`
- `PluginRegistration.Core.*.nupkg` + `.snupkg`
- `PluginRegistration.Tool.*.nupkg` + `.snupkg`

### Automatic publishing (GitHub Actions + Trusted Publishing)

Workflow: `.github/workflows/publish-nuget.yml`

- Builds, tests, packs all 3 packages
- Uses **Trusted Publishing** (OIDC)
- Publishes on GitHub Release or manual run with `publish=true`

One-time setup on nuget.org — Trusted Publisher per package:
- Owner: `crstPawel`
- Repository: `PluginRegistration`

### Manual push

**GitHub Packages:**

```bash
dotnet nuget push ./artifacts/*.nupkg \
  --api-key $GITHUB_TOKEN \
  --source "https://nuget.pkg.github.com/YOUR_ORG/index.json" \
  --skip-duplicate
```

**Azure Artifacts:**

```bash
dotnet nuget push ./artifacts/*.nupkg \
  --api-key $AZURE_ARTIFACTS_PAT \
  --source "https://pkgs.dev.azure.com/YOUR_ORG/_packaging/YOUR_FEED/nuget/v3/index.json" \
  --skip-duplicate
```

Always use `--skip-duplicate` for idempotent pipeline runs.

### Configure feed in consumer projects

**`NuGet.config` (recommended):**

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="my-internal-feed" value="https://pkgs.dev.azure.com/.../nuget/v3/index.json" />
  </packageSources>
</configuration>
```

**Tool install from private feed:**

```bash
dotnet tool install --global PluginRegistration.Tool --version 1.0.3 \
  --add-source https://pkgs.dev.azure.com/.../nuget/v3/index.json
```