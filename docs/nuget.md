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

- Builds, tests, packs the 3 packages under `src/` (never `Sample.Plugins`)
- Exchanges GitHub OIDC for a **short-lived NuGet API key** via [`NuGet/login@v1`](https://github.com/NuGet/login) (raw OIDC JWT is **not** a valid `--api-key`)
- Publishes on GitHub Release or manual `workflow_dispatch` with `publish=true`
- Optional fallback: classic long-lived `NUGET_API_KEY` if OIDC login fails

#### One-time setup

**Important:** GitHub login (`crstPawel`) ≠ nuget.org package owner (`psobczak`).  
`NuGet/login` `user` must be the **policy creator** on nuget.org (usually the package owner), not the GitHub username.  
Error `No matching trust policy owned by user '…'` almost always means wrong nuget.org username or missing policy.

**1. nuget.org — create Trusted Publishing policy** while logged in as **`psobczak`** (owner of the packages):

| Field | Value |
|-------|--------|
| Repository Owner | `crstPawel` (GitHub user/org of the repo) |
| Repository | `PluginRegistration` |
| Workflow File | `publish-nuget.yml` (filename only — no `.github/workflows/`) |
| Environment | leave empty (workflow does not use `environment:`) |

Policy must be created under the same account that owns:

- `PluginRegistration.Attributes`
- `PluginRegistration.Core`
- `PluginRegistration.Tool`

A new policy may stay *pending / temporary* for up to 7 days until the first successful publish activates it permanently.

**2. GitHub secrets / variables** (optional overrides)

| Name | Required | Value |
|------|----------|--------|
| `NUGET_USER` (secret or repo variable) | No | nuget.org **profile username** of the policy creator (not email). Workflow default: **`psobczak`** |
| `NUGET_API_KEY` (secret) | Optional fallback | Classic API key with Push to the packages above |

Do **not** set `NUGET_USER` to `crstPawel` unless that is also the nuget.org login that created the trust policy.

**3. Workflow permissions** (already set in YAML): `id-token: write`, `contents: read`.

#### How the auth flow works

```text
GitHub OIDC JWT  →  NuGet/login@v1  →  nuget.org /api/v2/token  →  temp NUGET_API_KEY (~1h)  →  dotnet nuget push
```

Do **not** pass the raw GitHub OIDC JWT to `dotnet nuget push --api-key` — nuget.org will return **403**.

#### Alternative (no Trusted Publishing)

1. nuget.org → API Keys → key with **Push** for `PluginRegistration.*` (or `*`).
2. GitHub → Secrets → `NUGET_API_KEY`.
3. You can omit `NUGET_USER`; the workflow falls back to the classic key (less secure: long-lived secret).

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