using System.Collections;
using System.Reflection;

namespace PluginRegistration.Core.Registration;

internal static class FilteringAttributesParser
{
    public static string Parse(CustomAttributeTypedArgument argument)
    {
        if (argument.Value is string text)
        {
            return text;
        }

        if (argument.ArgumentType.IsArray && argument.ArgumentType.GetElementType() == typeof(string))
        {
            return Join(ExtractStringArray(argument.Value));
        }

        return string.Empty;
    }

    private static string Join(IEnumerable<string> filteringAttributes)
    {
        var attributes = filteringAttributes
            .Where(attribute => !string.IsNullOrWhiteSpace(attribute))
            .ToArray();

        return attributes.Length == 0 ? string.Empty : string.Join(",", attributes);
    }

    private static string[] ExtractStringArray(object? value)
    {
        switch (value)
        {
            case string[] array:
                return array;
            case IList<CustomAttributeTypedArgument> typedArguments:
                return typedArguments
                    .Select(argument => (string)argument.Value!)
                    .ToArray();
            case IList list when list.Count > 0 && list[0] is CustomAttributeTypedArgument:
                return list
                    .Cast<CustomAttributeTypedArgument>()
                    .Select(argument => (string)argument.Value!)
                    .ToArray();
            case IList list:
                return list.Cast<string>().ToArray();
            default:
                return [];
        }
    }
}