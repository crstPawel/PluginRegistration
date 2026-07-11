using System;

namespace PluginRegistration.Attributes;

/// <summary>
/// Declares how a plugin type should be registered in Dataverse.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class CrmPluginRegistrationAttribute : Attribute
{
    /// <summary>
    /// Custom API registration — only the unique API name is required.
    /// </summary>
    public CrmPluginRegistrationAttribute(string message)
    {
        Message = message;
        IsolationMode = IsolationModeEnum.Sandbox;
    }

    public CrmPluginRegistrationAttribute(
        string message,
        string entityLogicalName,
        StageEnum stage,
        ExecutionModeEnum executionMode,
        string filteringAttributes,
        int executionOrder)
    {
        Message = message;
        EntityLogicalName = entityLogicalName;
        FilteringAttributes = filteringAttributes;
        ExecutionOrder = executionOrder;
        Stage = stage;
        ExecutionMode = executionMode;
        IsolationMode = IsolationModeEnum.Sandbox;
        Offline = false;
        Server = true;
    }

    public CrmPluginRegistrationAttribute(
        string message,
        string entityLogicalName,
        StageEnum stage,
        ExecutionModeEnum executionMode,
        string[] filteringAttributes,
        int executionOrder)
        : this(
            message,
            entityLogicalName,
            stage,
            executionMode,
            FilteringAttributesFormatter.Join(filteringAttributes),
            executionOrder)
    {
    }

    public CrmPluginRegistrationAttribute(
        MessageNameEnum message,
        string entityLogicalName,
        StageEnum stage,
        ExecutionModeEnum executionMode,
        string filteringAttributes,
        int executionOrder)
        : this(message.ToString(), entityLogicalName, stage, executionMode, filteringAttributes, executionOrder)
    {
    }

    public CrmPluginRegistrationAttribute(
        MessageNameEnum message,
        string entityLogicalName,
        StageEnum stage,
        ExecutionModeEnum executionMode,
        string[] filteringAttributes,
        int executionOrder)
        : this(message.ToString(), entityLogicalName, stage, executionMode, filteringAttributes, executionOrder)
    {
    }

    /// <summary>
    /// Custom workflow activity registration.
    /// </summary>
    public CrmPluginRegistrationAttribute(
        string name,
        string friendlyName,
        string description,
        string groupName,
        IsolationModeEnum isolationModel)
    {
        Name = name;
        FriendlyName = friendlyName;
        Description = description;
        GroupName = groupName;
        IsolationMode = isolationModel;
    }

    public string? Id { get; set; }
    public string? FriendlyName { get; set; }
    public string? GroupName { get; set; }
    public string? Description { get; set; }
    public bool DeleteAsyncOperation { get; set; }
    public string? UnSecureConfiguration { get; set; }
    public string? SecureConfiguration { get; set; }
    public bool Offline { get; set; }
    public bool Server { get; set; } = true;
    public PluginStepOperationEnum? Action { get; set; }

    /// <summary>
    /// Custom API binding type. Applies only to Custom API registrations.
    /// </summary>
    public CustomApiBindingTypeEnum CustomApiBindingType { get; set; } = CustomApiBindingTypeEnum.Global;

    /// <summary>
    /// When true, the Custom API is exposed as an OData Function (GET).
    /// </summary>
    public bool IsFunction { get; set; }

    /// <summary>
    /// When true, the Custom API is hidden from $metadata and code generation tools.
    /// </summary>
    public bool IsPrivate { get; set; }

    /// <summary>
    /// Required when <see cref="CustomApiBindingType"/> is Entity or EntityCollection.
    /// </summary>
    public string? BoundEntityLogicalName { get; set; }

    /// <summary>
    /// Controls whether other plug-in steps can extend this Custom API message.
    /// </summary>
    public CustomApiProcessingStepTypeEnum AllowedCustomProcessingStepType { get; set; } =
        CustomApiProcessingStepTypeEnum.None;

    public IsolationModeEnum IsolationMode { get; }
    public string? Message { get; }
    public string? EntityLogicalName { get; }
    public string? FilteringAttributes { get; }
    /// <summary>
    /// Step name override. When omitted, deploy resolves it as {namespace}.{class}.{stage}.
    /// </summary>
    public string? Name { get; set; }
    public int ExecutionOrder { get; }
    public StageEnum? Stage { get; }
    public ExecutionModeEnum ExecutionMode { get; }
}

public enum ExecutionModeEnum
{
    Asynchronous,
    Synchronous
}

public enum ImageTypeEnum
{
    PreImage = 0,
    PostImage = 1,
    Both = 2
}

public enum IsolationModeEnum
{
    None = 0,
    Sandbox = 1
}

public enum MessageNameEnum
{
    AddItem,
    AddListMembers,
    AddMember,
    AddMembers,
    AddPrincipalToQueue,
    AddPrivileges,
    AddProductToKit,
    AddRecurrence,
    AddToQueue,
    AddUserToRecordTeam,
    ApplyRecordCreationAndUpdateRule,
    Assign,
    Associate,
    BackgroundSend,
    Book,
    CalculatePrice,
    Cancel,
    CheckIncoming,
    CheckPromote,
    Clone,
    CloneMobileOfflineProfile,
    CloneProduct,
    Close,
    CopyDynamicListToStatic,
    CopySystemForm,
    Create,
    CreateException,
    CreateInstance,
    CreateKnowledgeArticleTranslation,
    CreateKnowledgeArticleVersion,
    Delete,
    DeleteOpenInstances,
    DeliverIncoming,
    DeliverPromote,
    Disassociate,
    Execute,
    ExecuteById,
    Export,
    GenerateSocialProfile,
    GetDefaultPriceLevel,
    GrantAccess,
    Import,
    LockInvoicePricing,
    LockSalesOrderPricing,
    Lose,
    Merge,
    ModifyAccess,
    PickFromQueue,
    Publish,
    PublishAll,
    PublishTheme,
    QualifyLead,
    Recalculate,
    ReleaseToQueue,
    RemoveFromQueue,
    RemoveItem,
    RemoveMember,
    RemoveMembers,
    RemovePrivilege,
    RemoveProductFromKit,
    RemoveRelated,
    RemoveUserFromRecordTeam,
    ReplacePrivileges,
    Reschedule,
    Retrieve,
    RetrieveExchangeRate,
    RetrieveFilteredForms,
    RetrieveMultiple,
    RetrievePersonalWall,
    RetrievePrincipalAccess,
    RetrieveRecordWall,
    RetrieveSharedPrincipalsAndAccess,
    RetrieveUnpublished,
    RetrieveUnpublishedMultiple,
    RetrieveUserQueues,
    RevokeAccess,
    RouteTo,
    Send,
    SendFromTemplate,
    SetLocLabels,
    SetRelated,
    SetState,
    TriggerServiceEndpointCheck,
    UnlockInvoicePricing,
    UnlockSalesOrderPricing,
    Update,
    ValidateRecurrenceRule,
    Win
}

public enum PluginStepOperationEnum
{
    Delete = 0,
    Deactivate = 1,
}

public enum StageEnum
{
    PreValidation = 10,
    PreOperation = 20,
    PostOperation = 40
}