# Documentation

Guides for installing, configuring, and using the PluginRegistration Tool.

## Getting started

1. [Installation](installation.md) — build or install `pluginreg`, add NuGet attributes to your plugin project
2. [Connection](connection.md) — configure Dataverse access (Service Principal, env vars)
3. [Getting started](getting-started.md) — quick start: attributes → pack → `deploy`
4. [CLI reference](cli.md) — commands and options

## Attributes & auth

- [Plugin steps](plugin-steps.md) — `[PluginRegistration]` and `[PluginStepImage]`
- [Custom API](custom-api.md) — `[CustomApiRegistration]` and request/response attributes
- [Authentication](authentication.md) — Client Secret, certificate, access token (WIF)

## Command internals

- [Deploy](deploy.md) — what happens during `pluginreg deploy`
- [Sync](sync.md) — what happens during `pluginreg sync`

## Operations

- [NuGet](nuget.md) — packages, feeds, and publishing
- [Azure DevOps](azure-devops.md) — pipelines, service connections, WIF
- [Troubleshooting](troubleshooting.md) — common errors and fixes
