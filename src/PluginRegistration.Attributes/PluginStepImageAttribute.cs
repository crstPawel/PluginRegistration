using System;

namespace PluginRegistration.Attributes
{
    /// <summary>
    /// Declares a pre/post image for a plugin step on the same class.
    /// Link the image to a step using <see cref="Stage"/> and optionally <see cref="Message"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class PluginStepImageAttribute : Attribute
    {
        public PluginStepImageAttribute(
            string name,
            ImageTypeEnum imageType,
            string attributes)
        {
            Name = name;
            ImageType = imageType;
            Attributes = attributes;
        }

        public string Name { get; }
        public ImageTypeEnum ImageType { get; }
        public string Attributes { get; }
    }
}