using System.Collections;
using System.Reflection;

namespace PluginRegistration.Core.Registration;

internal static class FilteringAttributesParser
{
    public static string[] ParseArray(CustomAttributeTypedArgument argument)
    {
        if (argument.Value is string text)
        {
            return string.IsNullOrWhiteSpace(text) ? [] : [text];
        }

        // MetadataLoadContext types are not identity-equal to runtime typeof(string).
        // Compare by name so string[] constructor args from plugin assemblies are recognized.
        if (argument.ArgumentType.IsArray && IsStringType(argument.ArgumentType.GetElementType()))
        {
            return ExtractStringArray(argument.Value);
        }

        // Some attribute encodings surface arrays only as IEnumerable of typed arguments.
        if (argument.Value is IList<CustomAttributeTypedArgument> typedArguments
            && typedArguments.Count > 0
            && IsStringType(typedArguments[0].ArgumentType))
        {
            return typedArguments
                .Select(item => (string)item.Value!)
                .ToArray();
        }

        return [];
    }

    private static bool IsStringType(Type? type)
        => type is not null
           && (type == typeof(string)
               || string.Equals(type.FullName, "System.String", StringComparison.Ordinal));

    public static string Parse(CustomAttributeTypedArgument argument)
        => Join(ParseArray(argument));

    public static string[] SplitCommaSeparated(string? attributes)
    {
        if (string.IsNullOrWhiteSpace(attributes))
        {
            return [];
        }

        return attributes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static string FormatForCode(string[] attributes)
    {
        // Pre-C# 12 array initializer — compatible with net462 plugin projects.
        // Use doubled braces only if this value is ever embedded in a string.Format format string.
        if (attributes.Length == 0)
        {
            return "new string[0]";
        }

        return $"new string[] {{ {string.Join(", ", attributes.Select(part => $"\"{part}\""))} }}";
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