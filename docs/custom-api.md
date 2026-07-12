# Custom API registration

Custom API is declared with a **separate** `[CustomApiRegistration]` attribute (not `[PluginRegistration]`).

On `deploy`, the tool:

1. Creates or updates `customapi`
2. Registers `customapirequestparameter` and `customapiresponseproperty`
3. Binds to `plugintypeid`
4. Adds components to the solution from `pluginregistration.json`

## Full definition in code

```csharp
using Microsoft.Xrm.Sdk;
using PluginRegistration.Attributes;

[CustomApiRegistration(
    "sample_ProcessAccount",
    FriendlyName = "Process Account",
    Description = "Processes the account identifier",
    CustomApiBindingType = CustomApiBindingTypeEnum.Global,
    IsFunction = false,
    IsPrivate = false,
    ProcessingStepType = CustomApiProcessingStepTypeEnum.None)]
[CustomApiRequestParameter(
    "AccountId",
    CustomApiParameterTypeEnum.String,
    IsRequired = true,
    Description = "Account identifier")]
[CustomApiResponseProperty(
    "Success",
    CustomApiParameterTypeEnum.Boolean,
    Description = "Whether the operation succeeded")]
public sealed class ProcessAccountCustomApiPlugin : IPlugin
{
    public void Execute(IServiceProvider serviceProvider)
    {
        // Read:  context.InputParameters["AccountId"]
        // Write: context.OutputParameters["Success"] = true;
    }
}
```

### `[CustomApiRegistration]`

| Property | Description |
|----------|-------------|
| `FriendlyName` / `DisplayName` | Display name |
| `Description` | API description |
| `CustomApiBindingType` | `Global`, `Entity`, `EntityCollection` |
| `BoundEntityLogicalName` | Required for entity binding |
| `IsFunction` | OData Function (GET) — **immutable after create** |
| `IsPrivate` | Hidden in $metadata |
| `ProcessingStepType` | `None`, `AsyncOnly`, `SyncAndAsync` |

### `[CustomApiRequestParameter]`

| Property | Description |
|----------|-------------|
| `UniqueName`, `Type` | Constructor arguments |
| `DisplayName`, `Description`, `IsRequired` | Optional |
| `EntityLogicalName` | For entity-related types |
| `ApiUniqueName` | Required when class has multiple Custom APIs |

### `[CustomApiResponseProperty]`

Same as request parameters except `IsRequired`.

Allowed types: `Boolean`, `DateTime`, `Decimal`, `Entity`, `EntityCollection`, `EntityReference`, `Float`, `Integer`, `Money`, `Picklist`, `String`, `Guid`, `StringArray`.

## Deploy behavior

| Situation | Action |
|-----------|--------|
| API does not exist | Create API, parameters, responses, plugin link |
| Editable field change | Update records |
| Parameter added/removed | Create/delete records |
| Immutable field change | Delete entire API tree and recreate |

Immutable fields: `bindingtype`, `isfunction`, `boundentitylogicalname`, parameter `type` / `logicalentityname`, `IsRequired` on existing request parameters.

## Minimal form

```csharp
[CustomApiRegistration("my_ProcessOrder")]
public class ProcessOrderApiPlugin : IPlugin
{
    public void Execute(IServiceProvider serviceProvider) { }
}
```

## JSON-only definition (fallback)

In `profiles.customApis` with `createIfMissing: true` — see [configuration.md](configuration.md).

Deploy order: assembly → plugin types → Custom APIs from code → JSON-only definitions.

## Sync

`pluginreg sync` writes `[CustomApiRegistration]`, request parameters, and response properties from Dataverse. See [sync.md](sync.md).

```bash
pluginreg sync --path ./src/MyPlugins
```