namespace InfraSweep.Discovery.Exceptions;

[Serializable]
public class NoIpV4NetworkException : Exception
{
    public NoIpV4NetworkException() { }
    public NoIpV4NetworkException(string message) : base(message) { }
    public NoIpV4NetworkException(string message, Exception inner) : base(message, inner) { }
}