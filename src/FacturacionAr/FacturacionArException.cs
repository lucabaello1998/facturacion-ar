namespace FacturacionAr;

public class FacturacionArException : Exception
{
    public FacturacionArException(string message) : base(message) { }
    public FacturacionArException(string message, Exception innerException) : base(message, innerException) { }
}
