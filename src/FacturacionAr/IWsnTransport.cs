namespace FacturacionAr;

/// <summary>
/// Abstracción del transporte SOAP. Permite inyectar un cliente HTTP real
/// o un fake en los tests sin tocar la red.
/// </summary>
public interface IWsnTransport
{
    Task<string> PostSoapAsync(
        string endpointUrl,
        string soapAction,
        string bodyXml,
        CancellationToken cancellationToken = default);
}
