namespace PluginRegistration.Core;

public interface ITrace
{
    void WriteLine(string format, params object?[] args);
}