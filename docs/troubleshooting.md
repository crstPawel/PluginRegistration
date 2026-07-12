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

See also:
- [connection.md](connection.md) — setup issues
- [deploy.md](deploy.md) — deploy pitfalls
- [sync.md](sync.md) — sync limitations