using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PluginRegistration.Core")]

namespace PluginRegistration.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class PluginRegistrationAttribute : Attribute
    {
        public PluginRegistrationAttribute(
            MessageTypeEnum message,
            string entityLogicalName,
            StageEnum stage,
            ExecutionModeEnum executionMode,
            string[] filteringAttributes,
            int executionOrder)
        {
            Message = message.ToString();
            EntityLogicalName = entityLogicalName;
            Stage = stage;
            ExecutionMode = executionMode;
            FilteringAttributes = filteringAttributes;
            ExecutionOrder = executionOrder;
        }

       internal static PluginRegistrationAttribute CreateStep(
            MessageTypeEnum message,
            string entityLogicalName,
            StageEnum stage,
            ExecutionModeEnum executionMode,
            string[] filteringAttributes,
            int executionOrder)
        {
            return new PluginRegistrationAttribute(
                message,
                entityLogicalName,
                stage,
                executionMode,
                filteringAttributes,
                executionOrder);
        }

        public string? Id { get; set; }
        public string? Message { get; }
        
        
        
        
        public bool DeleteAsyncOperation { get; set; }
        public string? UnSecureConfiguration { get; set; }
        public string? SecureConfiguration { get; set; }
        
        public bool Server { get; set; } = true;
        
        public PluginStepOperationEnum? Action { get; set; }

        public IsolationModeEnum IsolationMode { get; }

        public string? EntityLogicalName { get; }
        public string[] FilteringAttributes { get; }
        public string? Name { get; set; }
        public int ExecutionOrder { get; }
        public StageEnum? Stage { get; }
        public ExecutionModeEnum ExecutionMode { get; }
    }
}