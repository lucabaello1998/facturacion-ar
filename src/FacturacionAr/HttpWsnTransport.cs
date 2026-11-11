using System.Text;

namespace FacturacionAr;

public sealed class HttpWsnTransport : IWsnTransport, IDisposable
{
    private readonly HttpClient _httpClient;

    public HttpWsnTransport(HttpClient? httpClient = null)
    {
        // Sin timeout, una AFIP colgada deja el proceso esperando para siempre.
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<string> PostSoapAsync(
        string endpointUrl,
        string soapAction,
        string bodyXml,
        CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(bodyXml, Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", soapAction);

        using var response = await _httpClient.PostAsync(endpointUrl, content, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        response.EnsureSuccessStatusCode();
        return responseText;
    }

    public void Dispose() => _httpClient.Dispose();
}
