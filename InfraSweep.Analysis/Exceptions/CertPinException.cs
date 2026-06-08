namespace InfraSweep.Analysis.Exceptions;

[Serializable]
public class CertPinException : Exception
{
    public CertPinException() { }
    public CertPinException(string message) : base(message) { }
    public CertPinException(string message, Exception inner) : base(message, inner) { }
}