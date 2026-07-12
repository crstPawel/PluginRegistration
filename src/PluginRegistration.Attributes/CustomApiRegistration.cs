using System;

namespace PluginRegistration.Attributes
{
    /// <summary>
    /// Declares a Custom API registration for a plugin class.
    /// Use this attribute to define a Custom API (custom message) that will be registered
    /// in Dataverse together with its plugin implementation.
    /// </summary>
    /// <remarks>
    /// This attribute can be applied multiple times on the same class when one plugin
    /// handles several Custom APIs.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class CustomApiRegistration : Attribute
    {
        /// <summary>
        /// Initializes a new instance using only the unique name.
        /// <see cref="DisplayName"/> will default to the same value.
        /// </summary>
        public CustomApiRegistration(string uniqueName)
        {
            ValidateUniqueName(uniqueName);   // runtime validation (in addition to compile-time analyzer)
            UniqueName = uniqueName;
            DisplayName = uniqueName;
        }

        /// <summary>
        /// Determines whether the specified string is a valid value for <see cref="UniqueName"/>.
        /// A valid unique name may contain only letters (a-z, A-Z), digits (0-9) and the underscore (_) character.
        /// It cannot be null, empty or contain whitespace.
        /// </summary>
        public static bool IsValidUniqueName(string? uniqueName)
            => CustomApiValidation.IsValidUniqueName(uniqueName);

        /// <inheritdoc cref="CustomApiValidation.IsValidUniqueName(string?)"/>
        /// <param name="uniqueName">The value to validate.</param>
        /// <param name="errorMessage">When the method returns <c>false</c>, contains a description of the validation problem; otherwise <c>null</c>.</param>
        public static bool IsValidUniqueName(string? uniqueName, out string? errorMessage)
        {
            errorMessage = null;

            if (CustomApiValidation.IsValidUniqueName(uniqueName))
                return true;

            if (string.IsNullOrWhiteSpace(uniqueName))
            {
                errorMessage = "UniqueName cannot be null or whitespace.";
            }
            else
            {
                errorMessage = "UniqueName may only contain letters (a-z, A-Z), digits (0-9) and the underscore (_) character.";
            }

            return false;
        }

        private static void ValidateUniqueName(string uniqueName)
        {
            if (!IsValidUniqueName(uniqueName, out var errorMessage))
            {
                throw new ArgumentException(errorMessage ?? "UniqueName is invalid.", nameof(uniqueName));
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomApiRegistration"/> attribute
        /// with explicit unique name and display name.
        /// </summary>
        /// <param name="uniqueName">
        /// The unique name (logical name) of the Custom API.
        /// Must contain only letters (a-z, A-Z), digits (0-9) and the underscore (_) character.
        /// </param>
        /// <param name="displayName">
        /// The display name shown in Dataverse UI and in the $metadata document.
        /// </param>
        public CustomApiRegistration(string uniqueName, string displayName)
        {
            ValidateUniqueName(uniqueName);
            UniqueName = uniqueName;
            DisplayName = displayName;
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="CustomApiRegistration"/> attribute
        /// with binding configuration.
        /// </summary>
        /// <param name="uniqueName">
        /// The unique name of the Custom API.
        /// Must contain only letters (a-z, A-Z), digits (0-9) and the underscore (_) character.
        /// </param>
        /// <param name="displayName">The display name of the Custom API.</param>
        /// <param name="processingStepType">Allowed custom processing step type.</param>
        /// <param name="customApiBindingType">Binding scope of the Custom API.</param>
        /// <param name="boundEntityLogicalName">
        /// Logical name of the entity. Required when <paramref name="customApiBindingType"/>
        /// is <see cref="CustomApiBindingTypeEnum.Entity"/> or <see cref="CustomApiBindingTypeEnum.EntityCollection"/>.
        /// </param>
        public CustomApiRegistration(string uniqueName, string displayName, CustomApiProcessingStepTypeEnum processingStepType, CustomApiBindingTypeEnum customApiBindingType, string boundEntityLogicalName)
        {
            ValidateUniqueName(uniqueName);
            UniqueName = uniqueName;
            DisplayName = displayName;
            ProcessingStepType = processingStepType;
            CustomApiBindingType = customApiBindingType;
            BoundEntityLogicalName = boundEntityLogicalName;
        }
        
        /// <summary>
        /// Display name of the Custom API. Shown in the Dataverse user interface and in the Web API $metadata.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Alias for <see cref="DisplayName"/> for compatibility with other registration attributes.
        /// </summary>
        public string? FriendlyName
        {
            get => DisplayName;
            set => DisplayName = value;
        }

        /// <summary>
        /// Optional description of the Custom API.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// The unique (logical) name of the Custom API. This value is used as the message name
        /// when the API is invoked.
        /// <para>
        /// Must contain only letters (a-z, A-Z), digits (0-9) and the underscore (_) character.
        /// Validated automatically in the constructor.
        /// </para>
        /// </summary>
        public string UniqueName { get; }

        /// <summary>
        /// Optional name of a privilege that is required to execute this Custom API.
        /// Use an existing privilege name from the <c>Privilege</c> table.
        /// </summary>
        public string? ExecutePrivilegeName { get; set; }

        /// <summary>
        /// Logical name of the bound entity. Required when <see cref="CustomApiBindingType"/>
        /// is set to <see cref="CustomApiBindingTypeEnum.Entity"/> or <see cref="CustomApiBindingTypeEnum.EntityCollection"/>.
        /// </summary>
        public string? BoundEntityLogicalName { get; set; }

        /// <summary>
        /// Specifies which types of custom processing steps (plugins) are allowed to be registered
        /// against this Custom API message.
        /// <list type="bullet">
        /// <item><description><see cref="CustomApiProcessingStepTypeEnum.None"/> – No additional steps are allowed. The registered plugin is the only logic.</description></item>
        /// <item><description><see cref="CustomApiProcessingStepTypeEnum.AsyncOnly"/> – Only asynchronous steps are allowed.</description></item>
        /// <item><description><see cref="CustomApiProcessingStepTypeEnum.SyncAndAsync"/> – Both synchronous and asynchronous steps are allowed.</description></item>
        /// </list>
        /// </summary>
        public CustomApiProcessingStepTypeEnum ProcessingStepType { get; set; }

        /// <summary>
        /// Defines the binding scope of the Custom API.
        /// <list type="bullet">
        /// <item><description><see cref="CustomApiBindingTypeEnum.Global"/> – No entity binding (default behavior).</description></item>
        /// <item><description><see cref="CustomApiBindingTypeEnum.Entity"/> – Bound to a single entity record. Requires <see cref="BoundEntityLogicalName"/>.</description></item>
        /// <item><description><see cref="CustomApiBindingTypeEnum.EntityCollection"/> – Bound to a collection of entity records. Requires <see cref="BoundEntityLogicalName"/>.</description></item>
        /// </list>
        /// </summary>
        public CustomApiBindingTypeEnum CustomApiBindingType { get; set; }

        /// <summary>
        /// When set to <c>true</c>, the Custom API is registered as an OData Function (uses GET semantics).
        /// Functions must have at least one response property.
        /// <para><b>Important:</b> This value cannot be changed after the Custom API has been created.</para>
        /// </summary>
        public bool IsFunction { get; set; }

        /// <summary>
        /// When set to <c>true</c>, the Custom API is not exposed in the service metadata ($metadata).
        /// Useful for internal or system-only APIs.
        /// </summary>
        public bool IsPrivate { get; set; }
    }
}
