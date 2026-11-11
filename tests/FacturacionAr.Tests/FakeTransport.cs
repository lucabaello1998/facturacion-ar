namespace FacturacionAr.Tests;

public sealed class FakeTransport : IWsnTransport
{
    private readonly Func<string, string, string, string>? _handler;
    private readonly string? _fixedResponse;

    public FakeTransport(string fixedResponse)
    {
        _fixedResponse = fixedResponse;
    }

    public FakeTransport(Func<string, string, string, string> handler)
    {
        _handler = handler;
    }

    public Task<string> PostSoapAsync(string endpointUrl, string soapAction, string bodyXml, CancellationToken cancellationToken = default)
    {
        var response = _handler?.Invoke(endpointUrl, soapAction, bodyXml) ?? _fixedResponse!;
        return Task.FromResult(response);
    }
}
