# CLI reference

```bash
pluginreg --help
```

| Command | Description |
|---------|-------------|
| `pluginreg deploy` | Upload plugin NuGet package and register/update plugin steps and Custom APIs |
| `pluginreg sync` | Pull metadata from Dataverse and write attributes into `.cs` files |
| `pluginreg whoami` | Verify Dataverse connection |
| `pluginreg earlybound` | Generate early-bound types via DLaB EBG V2 (`earlyboundgenerator.xml`) |

---

## `deploy`

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--path` | `-p` | current directory | Working directory used to resolve relative package paths |
| `--package-path` | | `bin/Release` | Folder or pattern for `*.nupkg` (relative to `--path`) |
| `--solution` | `-s` | — | Dataverse solution unique name (components + publisher prefix) |
| `--connection` | `-c` | env vars | Connection string (alternative to `DATAVERSE_*`) |
| `--exclude-steps` | | `false` | Upload package only, skip step/Custom API registration |

```bash
pluginreg deploy --path ./src/MyPlugins --package-path bin/Release --solution MySolution
pluginreg deploy --path . --package-path bin/Release/*.nupkg --solution contoso_Plugins
pluginreg deploy --exclude-steps --package-path bin/Release --solution MySolution
pluginreg deploy -c "AuthType=ClientSecret;Url=https://org.crm4.dynamics.com;..."
```

Details: [deploy.md](deploy.md).

---

## `sync`

| Option | Description |
|--------|-------------|
| `--path` / `-p` | Directory with plugin `.cs` files (absolute path to another project OK) |
| `--connection` / `-c` | Connection string |
| `--class-regex` | Custom class detection regex |

```bash
pluginreg sync --path ./src/MyPlugins
pluginreg sync --path /home/user/MyCrmPlugins/src/MyCompany.Plugins
```

Overwrites registration attributes from current Dataverse state. Commit or back up before running.

**Class detection:** supports `BasePlugin : IPlugin` → `MyPlugin : BasePlugin` when all related `.cs` files are under `--path`.

Details: [sync.md](sync.md).

---

## `whoami`

```bash
pluginreg whoami
```

Returns `OrganizationId`, `BusinessUnitId`, and `UserId`.

---

## `earlybound`

Generates early-bound entities, option sets, and messages using **DLaB Early Bound Generator V2**.

Configuration is the **native DLaB XML** file (`earlyboundgenerator.xml` / `EarlyBoundGeneratorConfig`).  
The full schema is used as-is (`ExtensionConfig`, filters, camelCase, transliteration, service type names, etc.). There is no JSON config path.

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--path` | `-p` | current directory | Working directory (config path is relative to this) |
| `--config` | | `earlyboundgenerator.xml` under `--path` | Path to DLaB EBG V2 XML config |
| `--output` | `-o` | `EarlyBound` under `--path` (or `RootPath` in XML) | Output directory override |
| `--namespace` | `-n` | from XML | Namespace override |
| `--service-context` | | from XML | Service context class name override |
| `--entities` | `-e` | from XML | Pipe-separated entity whitelist override |
| `--skip-messages` | | from XML | Force `GenerateMessages=false` |
| `--global-option-sets` | | from XML | Force `GenerateGlobalOptionSets=true` |
| `--overwrite` | | from XML | Clear read-only + delete files in output folders |
| `--init-config` | | | Write default DLaB `earlyboundgenerator.xml` and exit |
| `--force` | | | With `--init-config`, replace existing XML |
| `--connection` | `-c` | env vars | Dataverse connection (required unless `--init-config`) |

```bash
# Scaffold native DLaB XML (full defaults from EarlyBoundGeneratorConfig.GetDefault)
pluginreg earlybound --path ./src/MyPlugins --init-config

# Generate using earlyboundgenerator.xml in --path
pluginreg earlybound --path ./src/MyPlugins --overwrite

# Explicit config + CLI overrides
pluginreg earlybound --config ./configs/earlyboundgenerator.xml -e "account|contact" -n MyCompany.Model
```

CLI flags only override the matching fields; every other setting is taken from the XML.
