using System.Security.Cryptography.X509Certificates;

namespace FacturacionAr;

public sealed class CliFacturacion
{
    private readonly WsaaClient _wsaa;
    private readonly Wsfev1Client _wsfe;
    private readonly string _wsaaUrl;
    private readonly string _wsfeUrl;
    private readonly long _cuit;

    public CliFacturacion(
        IWsnTransport transport,
        X509Certificate2 cert,
        string wsaaUrl,
        string wsfeUrl,
        long cuit)
    {
        _wsaa = new WsaaClient(transport, cert);
        _wsfe = new Wsfev1Client(transport);
        _wsaaUrl = wsaaUrl;
        _wsfeUrl = wsfeUrl;
        _cuit = cuit;
    }

    public async Task<FecaeSolicitarResponse> SolicitarCaeAsync(Comprobante comprobante, CancellationToken ct = default)
    {
        var creds = await _wsaa.AuthenticateAsync(_wsaaUrl, ct);
        return await _wsfe.FecaeSolicitarAsync(_wsfeUrl, creds, _cuit, comprobante, ct);
    }

    public async Task<long> ObtenerUltimoAutorizadoAsync(
        TipoComprobante tipo,
        int puntoVenta,
        CancellationToken ct = default)
    {
        var creds = await _wsaa.AuthenticateAsync(_wsaaUrl, ct);
        return await _wsfe.FeCompUltimoAutorizadoAsync(_wsfeUrl, creds, _cuit, (int)tipo, puntoVenta, ct);
    }
}
