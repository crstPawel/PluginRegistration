# Troubleshooting

| Problem | Solution |
|---------|----------|
| `Package path not found` / no packages found | Run `dotnet pack -c Release`; pass `--package-path` to the folder with `*.nupkg` (default `bin/Release` under `--path`) |
| `Unable to connect to Dataverse` | Run `whoami`; check URL, Client ID/Secret, Tenant ID, Application User |
| `Could not load file or assembly 'System.ServiceModel'` | Rebuild after updating the repo — WCF packages are in `PluginRegistration.Core` |
| `Missing environment variables` | Set `DATAVERSE_*` or use `--connection` |
| `sync` does not modify files | Plugins must exist in Dataverse; type names must match `plugintype.typename` |
| `sync` skips BasePlugin class | Put `BasePlugin.cs` and derived classes under the same `--path`; try `--class-regex` |
| `Custom API 'X' not found` | Define `[CustomApiRegistration]` (and parameters) on an `IPlugin` class included in the deployed package |
| Custom API recreated from scratch | Normal when changing parameter type or `IsFunction` |
| `Duplicate Custom API request parameter names` | Unique names per parameter/response on the class |
| `Duplicate plugin step names` | Unique step names per class (auto or explicit `Name = "..."`) |
| Plugin not detected on `deploy` | Class must implement `IPlugin` (directly or via base); ensure the type is in the packed `.nupkg` under `lib/` |
| `PluginType … not found in PluginAssembly … total of [0] types` | Package-managed assemblies only use plugintypes created by Dataverse from the `.nupkg`. Re-deploy with the fixed tool; if a previous run created orphan `plugintype` rows, unregister the package (or delete bad types/steps) and deploy again. Confirm DLL is under `lib/` and implements `IPlugin`. |
| `did not expose expected plugintypes after package upload` | Server never registered types from the package — check package layout (`lib/net462/...`), target framework, and that types implement `IPlugin` |
| `Unable to delete '…' plugintype due to N step(s) registered on it` | Fixed in current tool: steps/Custom APIs for types leaving the package are deleted **before** package content update. Re-deploy with the updated tool. If the error persists from a partial run, delete remaining steps for that type in Plugin Registration / XrmToolBox, then re-deploy. |
| `Export key attribute uniquename for component CustomAPI must start with a valid customization prefix` | Pass `--solution` so deploy prefixes Custom API unique names with that solution's **publisher customization prefix**. Names already starting with `{prefix}_` are left unchanged. |
| `Cannot add connector with id …` when adding Custom API to a solution | Classic component types **371/372 are Connector**, not Custom API. The tool resolves `customapi` **ObjectTypeCode** from metadata and uses that for `AddSolutionComponent`, plus `Create`/`Update` with `SolutionUniqueName`. Rebuild and re-deploy. |
| Only some Custom API parameters created / parameters disappear after deploy | Older builds re-applied JSON `customApis` with empty parameters and wiped attribute-registered ones. `customApis` is removed from config — re-deploy with attributes as the only source of truth. |
| Unknown message on `sync` | Add the SDK message to `MessageTypeEnum` in the Attributes package |

See also:
- [connection.md](connection.md) — setup issues
- [deploy.md](deploy.md) — deploy pitfalls
- [sync.md](sync.md) — sync limitations