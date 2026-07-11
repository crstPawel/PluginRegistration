using System;
using System.Linq;

namespace PluginRegistration.Attributes;

internal static class FilteringAttributesFormatter
{
    public static string Join(params string[] filteringAttributes)
    {
        if (filteringAttributes is null || filteringAttributes.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(
            ",",
            filteringAttributes.Where(attribute => !string.IsNullOrWhiteSpace(attribute)));
    }
}