# Troubleshooting

| Problem | Solution |
|---------|----------|
| `Configuration file not found` | For `deploy`/`init` — place `pluginregistration.json` in `--path`. `sync` does not need it |
| `Assembly path not found` | Run `dotnet build -c Release` before `deploy` |
| `Unable to connect to Dataverse` | Run `whoami`; check URL, Client ID/Secret, Tenant ID, Application User |
| `Could not load file or assembly 'System.ServiceModel'` | Rebuild after updating the repo — WCF packages are in `PluginRegistration.Core` |
| `Missing environment variables` | Set `DATAVERSE_*` or use `--connection` |
| `Environment variable 'X' is not set` | Set the variable locally or in the pipeline (used in `${X}` in JSON) |
| `sync` does not modify files | Plugins must exist in Dataverse; type names must match `plugintype.typename` |
| `sync` skips BasePlugin class | Put `BasePlugin.cs` and derived classes under the same `--path`; try `--class-regex` |
| `Custom API 'X' not found` | Define in code or use `createIfMissing: true` in `profiles.customApis` |
| Custom API recreated from scratch | Normal when changing parameter type or `IsFunction` |
| `Duplicate Custom API request parameter names` | Unique names per parameter/response on the class |
| `Duplicate plugin step names` | Unique step names per class (auto or explicit `Name = "..."`) |
| Plugin not detected on `deploy` | Class must implement `IPlugin` (directly or via base); check `assemblyPath` |
| Unknown message on `sync` | Add the SDK message to `MessageTypeEnum` in the Attributes package |
| `NU3034` / `Package signature validation failed` on `dotnet tool install` | NuGet.org repository certificate not in your trusted signers list — see below |

### NU3034 — package signing on Windows

Packages from nuget.org are **repository-signed** by Microsoft. Error `NU3034` usually means your machine has `signatureValidationMode=require` in `nuget.config` with an **outdated** nuget.org certificate fingerprint (common after the [2024 certificate rotation](https://devblogs.microsoft.com/dotnet/the-nuget-org-repository-signing-certificate-will-be-updated-as-soon-as-april-8th-2024/)).

This is a **client configuration** issue, not a broken package on nuget.org.

**Fix (PowerShell):**

```powershell
dotnet nuget trust repository nuget.org all
```

Or trust the current certificate fingerprint from the error message:

```powershell
dotnet nuget trust repository nuget.org --fingerprint 1F4B311D9ACC115C8DC8018B5A49E00FCE6DA8E2855F9F014CA6F34570BC482D
```

Then retry:

```powershell
dotnet tool install --global PluginRegistration.Tool --version 2.0.1
```

Also ensure .NET SDK **10.0** (see `global.json`). Check for `signatureValidationMode` in `%APPDATA%\NuGet\NuGet.Config` or a solution-level `nuget.config`.

See also:
- [connection.md](connection.md) — setup issues
- [deploy.md](deploy.md) — deploy pitfalls
- [sync.md](sync.md) — sync limitations