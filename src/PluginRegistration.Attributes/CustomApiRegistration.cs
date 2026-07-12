using System;

namespace PluginRegistration.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class CustomApiRegistration : Attribute
    {
        public CustomApiRegistration(string uniqueName)
        {
            this.UniqueName = uniqueName;
        }

        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public string UniqueName { get; }
        public string? ExecutePrivilegeName { get; set; }
        public string? BoundEntityLogicalName { get; set; }
        public CustomApiProcessingStepTypeEnum ProcessingStepType { get; set; } = CustomApiProcessingStepTypeEnum.None;
        public CustomApiBindingTypeEnum CustomApiBindingType { get; set; } = CustomApiBindingTypeEnum.Global;
        public bool IsFunction { get; set; }
        public bool IsPrivate { get; set; }
    }
}
