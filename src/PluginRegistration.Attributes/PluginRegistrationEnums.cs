namespace PluginRegistration.Attributes
{
    /// <summary>
    /// Common SDK messages for plugin registration.
    /// Use this enum instead of raw strings for better type safety and discoverability.
    /// For less common messages, use MessageNameEnum.
    /// </summary>
    public enum MessageTypeEnum
    {
        // Core entity operations
        Create,
        Update,
        Delete,
        Retrieve,
        RetrieveMultiple,

        // Assignment and sharing
        Assign,
        GrantAccess,
        ModifyAccess,
        RevokeAccess,

        // Associations
        Associate,
        Disassociate,

        // State changes
        SetState,
        Close,
        Win,
        Lose,
        Cancel,

        // Lead / Opportunity
        QualifyLead,

        // Merging
        Merge,

        // List / Team membership
        AddMember,
        RemoveMember,
        AddListMembers,
        RemoveListMembers,
        AddMembers,
        RemoveMembers,

        // Queue operations
        AddToQueue,
        RemoveFromQueue,
        PickFromQueue,
        RouteTo,

        // Other common
        Send,
        DeliverPromote,
        Reschedule,
        Book,
        Execute,
        MergeDuplicates,
        Clone,
        Publish,
        PublishAll
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
        /// <summary>
        /// Full trust / no isolation. Not allowed in many Dataverse environments.
        /// Dataverse value: 1
        /// </summary>
        None = 1,

        /// <summary>
        /// Sandboxed / isolated execution. Recommended default.
        /// Dataverse value: 2
        /// </summary>
        Sandbox = 2,

        /// <summary>
        /// External (for plugin packages in some scenarios).
        /// Dataverse value: 3
        /// </summary>
        External = 3
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
}
