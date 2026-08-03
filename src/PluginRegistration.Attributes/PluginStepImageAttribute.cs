using System;

namespace PluginRegistration.Attributes
{
    /// <summary>
    /// Declares a pre/post image for a plugin step on the same class.
    /// Images are matched to steps by image type (PreImage → pre-stages, PostImage → PostOperation)
    /// and optionally by <see cref="Message"/> when a class registers multiple steps.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class PluginStepImageAttribute : Attribute
    {
        public PluginStepImageAttribute(
            string name,
            ImageTypeEnum imageType,
            string[] attributes)
        {
            Name = name;
            ImageType = imageType;
            Attributes = attributes;
        }

        public string Name { get; }
        public ImageTypeEnum ImageType { get; }
        public string[] Attributes { get; }

        /// <summary>
        /// Optional SDK message name. Use when the class registers multiple steps.
        /// </summary>
        public string? Message { get; set; }
    }
}