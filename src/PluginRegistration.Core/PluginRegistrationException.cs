namespace PluginRegistration.Core;

public sealed class PluginRegistrationException : Exception
{
    public PluginRegistrationException(string message) : base(message)
    {
    }

    public PluginRegistrationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}