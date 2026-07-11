using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

namespace PluginRegistration.Tool.Cli;

internal static class CliErrorReporter
{
    public static void ReportValidationErrors(CommandResult result, IReadOnlyList<string> errors)
    {
        result.ErrorMessage = string.Join(Environment.NewLine, errors.Select(error => $"error: {error}"));
    }

    public static int ReportException(Exception exception, InvocationContext context)
    {
        var message = exception switch
        {
            PluginRegistration.Core.PluginRegistrationException pluginError => pluginError.Message,
            DirectoryNotFoundException => $"Directory not found: {exception.Message}",
            FileNotFoundException => exception.Message,
            ArgumentException argumentError => argumentError.Message,
            _ => exception.Message
        };

        WriteError(message);
        WriteCommandHelp(context.ParseResult.CommandResult.Command);
        return 1;
    }

    public static void WriteError(string message)
    {
        Console.Error.WriteLine($"error: {message}");
        Console.Error.WriteLine();
    }

    public static void WriteCommandHelp(Command command)
    {
        var helpBuilder = new HelpBuilder(LocalizationResources.Instance);
        helpBuilder.Write(command, Console.Out);
    }
}