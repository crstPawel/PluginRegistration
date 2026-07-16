# Plugin step registration

Step structure (message, entity, stage, mode, images) is defined **in code**. On `deploy`, the tool:

1. Uploads/updates `pluginassembly` (DLL as base64)
2. Registers/updates `plugintype` per class with attributes
3. Creates/updates `sdkmessageprocessingstep` and images
4. Removes steps no longer declared in attributes
5. Applies `stepOverrides` from the active profile
6. Optionally adds components to the solution

## Basic attribute

```csharp
[PluginRegistration(
    MessageTypeEnum.Create,
    "account",
    StageEnum.PostOperation,
    ExecutionModeEnum.Synchronous,
    ["name"],
    1)]
public class AccountCreatePlugin : IPlugin { ... }
```

Constructor parameters: `message`, `entityLogicalName`, `stage`, `executionMode`, `filteringAttributes` (`string[]`), `executionOrder`.

Named properties: `Id`, `Name`, `DeleteAsyncOperation`, `UnSecureConfiguration`, `SecureConfiguration`, `Server`, `Action`.

## Step with image and stable GUID

```csharp
[PluginRegistration(
    MessageTypeEnum.Update,
    "account",
    StageEnum.PostOperation,
    ExecutionModeEnum.Synchronous,
    ["name", "telephone1"],
    1,
    Id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
[PluginStepImage("PostImage", ImageTypeEnum.PostImage, ["name", "telephone1"])]
public class AccountUpdatePlugin : IPlugin { ... }
```

`Id` binds the step to a fixed GUID — useful across environments.

## Step naming

If `Name` is omitted, deploy generates:

`{namespace}.{class_name}.{StageEnum}`

Example: `Sample.Plugins.AccountCreatePlugin.PreOperation`

Custom name when multiple steps share the same stage:

```csharp
[PluginRegistration(MessageTypeEnum.Create, "account", StageEnum.PreOperation, ExecutionModeEnum.Synchronous, [], 1)]
[PluginRegistration(MessageTypeEnum.Update, "account", StageEnum.PreOperation, ExecutionModeEnum.Synchronous, ["name"], 1,
    Name = "MyNamespace.MyPlugin.UpdatePreOperation")]
```

## Step images

`[PluginStepImage(name, imageType, attributes)]` — `attributes` is a `string[]` (same style as `filteringAttributes` on `[PluginRegistration]`). Matched to steps by image type and stage (PreImage → pre-stages, PostImage → PostOperation).

Deploy internals: [deploy.md](deploy.md). Sync behavior: [sync.md](sync.md).