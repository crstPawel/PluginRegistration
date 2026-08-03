# Custom API registration

Custom API is declared with a **separate** `[CustomApiRegistration]` attribute (not `[PluginRegistration]`).

On `deploy`, the tool:

1. Creates or updates `customapi`
2. Registers `customapirequestparameter` and `customapiresponseproperty`
3. Binds to `plugintypeid`
4. Adds components to the solution when `--solution` is set

### Unique names and publisher prefix

Dataverse requires Custom API (and parameter) `uniquename` values to start with a **valid publisher customization prefix**.

When `--solution` is set on deploy, the tool resolves that solution's **publisher customization prefix** and applies it to the Custom API `uniquename` automatically:

| Name in attribute / JSON | Solution publisher prefix | Registered `uniquename` |
|--------------------------|---------------------------|-------------------------|
| `ProcessAccount` | `contoso` | `contoso_ProcessAccount` |
| `contoso_ProcessAccount` | `contoso` | `contoso_ProcessAccount` (no double prefix) |

The prefix always comes from the configured solution's publisher — not from hard-coded defaults. If the name already starts with `{prefix}_`, it is left unchanged.

Request/response parameter unique names (e.g. `AccountId`) are **not** rewritten — they stay as written so plugin `InputParameters` / `OutputParameters` keys match the code.

## Full definition in code

```csharp
using Microsoft.Xrm.Sdk;
using PluginRegistration.Attributes;

// Custom API unique name may omit the publisher prefix — deploy adds it from --solution.
[CustomApiRegistration(
    "ProcessAccount",
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

Custom APIs are registered only from code attributes during package deploy.

Deploy order: package upload → plugin types from package → Custom APIs / steps from attributes.

## Sync

`pluginreg sync` writes `[CustomApiRegistration]`, request parameters, and response properties from Dataverse. See [sync.md](sync.md).

```bash
pluginreg sync --path ./src/MyPlugins
```