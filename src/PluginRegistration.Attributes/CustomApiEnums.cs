namespace PluginRegistration.Attributes
{
    /// <summary>
    /// Contains compile-time enforceable rules for Custom API attribute values.
    /// </summary>
    public static class CustomApiValidation
    {
        /// <summary>
        /// Regular expression pattern for valid Custom API UniqueName values.
        /// Allowed characters: letters (a-z, A-Z), digits (0-9) and underscore (_).
        /// </summary>
        public const string UniqueNamePattern = @"^[a-zA-Z0-9_]+$";

        /// <summary>
        /// Checks whether the given string matches the rules for a Custom API unique name.
        /// This is the same rule used by the Roslyn analyzer to produce compile-time errors.
        /// </summary>
        public static bool IsValidUniqueName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            // value is guaranteed non-null here
            string name = value!;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Specifies the binding scope for a Custom API.
    /// </summary>
    public enum CustomApiBindingTypeEnum
    {
        /// <summary>
        /// Global binding. The Custom API is not bound to any specific entity.
        /// This is the most common option.
        /// </summary>
        Global = 0,

        /// <summary>
        /// Entity binding. The Custom API is bound to a single record of the specified entity.
        /// Requires <c>BoundEntityLogicalName</c> to be set.
        /// </summary>
        Entity = 1,

        /// <summary>
        /// EntityCollection binding. The Custom API operates on a collection of entity records.
        /// Requires <c>BoundEntityLogicalName</c> to be set.
        /// </summary>
        EntityCollection = 2
    }

    public enum CustomApiParameterTypeEnum
    {
        Boolean = 0,
        DateTime = 1,
        Decimal = 2,
        Entity = 3,
        EntityCollection = 4,
        EntityReference = 5,
        Float = 6,
        Integer = 7,
        Money = 8,
        Picklist = 9,
        String = 10,
        Guid = 12,
        StringArray = 13
    }

    /// <summary>
    /// Controls which additional plugin steps (custom processing) can be registered
    /// against a Custom API message by other developers.
    /// </summary>
    public enum CustomApiProcessingStepTypeEnum
    {
        /// <summary>
        /// No custom processing steps are allowed.
        /// The plugin registered on the Custom API is the only logic that will execute.
        /// Use this when you do not want anyone to extend or intercept the operation.
        /// </summary>
        None = 0,

        /// <summary>
        /// Only asynchronous plugin steps are allowed to be registered against this Custom API.
        /// </summary>
        AsyncOnly = 1,

        /// <summary>
        /// Both synchronous and asynchronous plugin steps are allowed.
        /// This is the most permissive option.
        /// </summary>
        SyncAndAsync = 2
    }
}