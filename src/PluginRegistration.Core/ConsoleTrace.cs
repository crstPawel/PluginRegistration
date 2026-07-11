namespace PluginRegistration.Core;

public sealed class ConsoleTrace : ITrace
{
    public void WriteLine(string format, params object?[] args)
    {
        Console.WriteLine(format, args);
    }
}