# CLI reference

```bash
pluginreg --help
```

| Command | Description |
|---------|-------------|
| `pluginreg init` | Generate `pluginregistration.json` from source code |
| `pluginreg deploy` | Upload assembly and register/update plugin steps and Custom APIs |
| `pluginreg sync` | Pull metadata from Dataverse and write attributes into `.cs` files |
| `pluginreg whoami` | Verify Dataverse connection |

---

## `deploy`

| Option | Short | Description |
|--------|-------|-------------|
| `--path` | `-p` | Directory with `pluginregistration.json` and assembly (default: current) |
| `--profile` | `-pr` | Environment profile: `dev`, `test`, `prod`, etc. |
| `--connection` | `-c` | Connection string (alternative to env vars) |
| `--exclude-steps` | | Update assembly only, skip step registration |

```bash
pluginreg deploy --path ./src/MyPlugins --profile test
pluginreg deploy --profile prod --exclude-steps
pluginreg deploy -c "AuthType=ClientSecret;Url=https://org.crm4.dynamics.com;..." --profile dev
```

> Custom workflow activity registration is **not supported** in the current attribute model. The legacy `--workflow` flag is ignored.

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

## `init`

| Option | Default | Description |
|--------|---------|-------------|
| `--path` / `-p` | current directory | Plugin project directory |
| `--profiles` | `dev,test,prod` | Comma-separated profiles |
| `--assembly-path` | `bin/Release` | DLL path for `plugins[].assemblyPath` |
| `--solution` | — | Solution name |
| `--force` | `false` | Overwrite existing `pluginregistration.json` |

Details: [init.md](init.md).

---

## `whoami`

```bash
pluginreg whoami
```

Returns `OrganizationId`, `BusinessUnitId`, and `UserId`.