using System.Globalization;
using System.Xml.Linq;

namespace FacturacionAr;

public sealed class Wsfev1Client
{
    private readonly IWsnTransport _transport;

    public Wsfev1Client(IWsnTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public async Task<FecaeSolicitarResponse> FecaeSolicitarAsync(
        string endpointUrl,
        WsaaCredentials credentials,
        long cuit,
        Comprobante comprobante,
        CancellationToken ct = default)
    {
        comprobante.Validar();

        var soap = BuildFecaeSolicitarSoap(credentials, cuit, comprobante);
        const string soapAction = "http://ar.gov.afip.dif.FEV1/FECAESolicitar";

        var responseXml = await _transport.PostSoapAsync(endpointUrl, soapAction, soap, ct);
        return ParseFecaeSolicitarResponse(responseXml);
    }

    public async Task<long> FeCompUltimoAutorizadoAsync(
        string endpointUrl,
        WsaaCredentials credentials,
        long cuit,
        int tipoComprobante,
        int puntoVenta,
        CancellationToken ct = default)
    {
        var soap = BuildFeCompUltimoAutorizadoSoap(credentials, cuit, tipoComprobante, puntoVenta);
        const string soapAction = "http://ar.gov.afip.dif.FEV1/FECompUltimoAutorizado";

        var responseXml = await _transport.PostSoapAsync(endpointUrl, soapAction, soap, ct);
        return ParseFeCompUltimoAutorizadoResponse(responseXml);
    }

    internal static string BuildFecaeSolicitarSoap(WsaaCredentials credentials, long cuit, Comprobante comp)
    {
        XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
        XNamespace fe = "http://ar.gov.afip.dif.FEV1/";

        var docTipo = comp.ReceptorCuit.HasValue ? "80" : "99";
        var docNro = comp.ReceptorCuit?.ToString() ?? "0";
        var fecha = comp.Fecha.ToString("yyyyMMdd");
        var ivaTotal = (comp.AlicuotasIva ?? Array.Empty<AlicuotaIva>()).Sum(a => a.Importe);

        var detalle = new XElement(fe + "FECAEDetRequest",
            new XElement(fe + "Concepto", "1"),
            new XElement(fe + "DocTipo", docTipo),
            new XElement(fe + "DocNro", docNro),
            new XElement(fe + "CbteDesde", comp.Numero),
            new XElement(fe + "CbteHasta", comp.Numero),
            new XElement(fe + "CbteFch", fecha),
            new XElement(fe + "ImpTotal", FormatoImporte(comp.ImporteTotal)),
            new XElement(fe + "ImpTotConc", "0"),
            new XElement(fe + "ImpNeto", FormatoImporte(comp.ImporteNeto)),
            new XElement(fe + "ImpOpEx", FormatoImporte(comp.ImporteExentoIva)),
            new XElement(fe + "ImpTrib", "0"),
            new XElement(fe + "ImpIVA", FormatoImporte(ivaTotal)));

        if (comp.AlicuotasIva is { Count: > 0 })
        {
            var ivaNode = new XElement(fe + "Iva");
            foreach (var a in comp.AlicuotasIva)
            {
                ivaNode.Add(new XElement(fe + "AlicIva",
                    new XElement(fe + "Id", a.Id),
                    new XElement(fe + "BaseImp", FormatoImporte(a.BaseImponible)),
                    new XElement(fe + "Importe", FormatoImporte(a.Importe))));
            }

            detalle.Add(ivaNode);
        }

        var envelope = new XElement(soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", soap.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "fe", fe.NamespaceName),
            new XElement(soap + "Body",
                new XElement(fe + "FECAESolicitar",
                    BuildAuth(fe, credentials, cuit),
                    new XElement(fe + "FeCAEReq",
                        new XElement(fe + "FeCabReq",
                            new XElement(fe + "CantReg", "1"),
                            new XElement(fe + "PtoVta", comp.PuntoVenta),
                            new XElement(fe + "CbteTipo", (int)comp.Tipo)),
                        new XElement(fe + "FeDetReq", detalle)))));

        return envelope.ToString();
    }

    internal static string BuildFeCompUltimoAutorizadoSoap(
        WsaaCredentials credentials,
        long cuit,
        int tipoComprobante,
        int puntoVenta)
    {
        XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
        XNamespace fe = "http://ar.gov.afip.dif.FEV1/";

        var envelope = new XElement(soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", soap.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "fe", fe.NamespaceName),
            new XElement(soap + "Body",
                new XElement(fe + "FECompUltimoAutorizado",
                    BuildAuth(fe, credentials, cuit),
                    new XElement(fe + "PtoVta", puntoVenta),
                    new XElement(fe + "CbteTipo", tipoComprobante))));

        return envelope.ToString();
    }

    private static XElement BuildAuth(XNamespace ns, WsaaCredentials credentials, long cuit)
    {
        return new XElement(ns + "Auth",
            new XElement(ns + "Token", credentials.Token),
            new XElement(ns + "Sign", credentials.Sign),
            new XElement(ns + "Cuit", cuit));
    }

    internal static FecaeSolicitarResponse ParseFecaeSolicitarResponse(string responseXml)
    {
        var doc = XDocument.Parse(responseXml);
        var body = doc.Descendants().First(e => e.Name.LocalName == "Body");
        var result = body.Descendants().First(e => e.Name.LocalName == "FECAESolicitarResult");

        var detResponse = result.Descendants().FirstOrDefault(e => e.Name.LocalName == "FECAEDetResponse");
        var resultado = detResponse?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Resultado")?.Value
                        ?? result.Descendants().FirstOrDefault(e => e.Name.LocalName == "Resultado")?.Value
                        ?? "R";

        var cae = detResponse?.Descendants().FirstOrDefault(e => e.Name.LocalName == "CAE")?.Value;

        DateTime? vencimiento = null;
        var vtoText = detResponse?.Descendants().FirstOrDefault(e => e.Name.LocalName == "CAEFchVto")?.Value;
        if (!string.IsNullOrEmpty(vtoText) && DateTime.TryParseExact(vtoText, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var vto))
            vencimiento = vto;

        var errores = ParseErrors(result).ToList().AsReadOnly();
        var observaciones = ParseObservations(result).ToList().AsReadOnly();

        return new FecaeSolicitarResponse(resultado, cae, vencimiento, errores, observaciones);
    }

    internal static long ParseFeCompUltimoAutorizadoResponse(string responseXml)
    {
        var doc = XDocument.Parse(responseXml);
        var body = doc.Descendants().First(e => e.Name.LocalName == "Body");
        var result = body.Descendants().First(e => e.Name.LocalName == "FECompUltimoAutorizadoResult");
        var cbteNroText = result.Descendants().FirstOrDefault(e => e.Name.LocalName == "CbteNro")?.Value;

        if (string.IsNullOrEmpty(cbteNroText) || !long.TryParse(cbteNroText, out var cbteNro))
            throw new FacturacionArException("No se pudo parsear el último comprobante autorizado.");

        return cbteNro;
    }

    private static IEnumerable<ErrorItem> ParseErrors(XContainer result)
    {
        var errors = result.Descendants().FirstOrDefault(e => e.Name.LocalName == "Errors");
        if (errors is null) yield break;

        foreach (var err in errors.Descendants().Where(e => e.Name.LocalName == "Err"))
        {
            var codeText = err.Descendants().FirstOrDefault(e => e.Name.LocalName == "Code")?.Value;
            var msg = err.Descendants().FirstOrDefault(e => e.Name.LocalName == "Msg")?.Value ?? "";
            if (int.TryParse(codeText, out var code))
                yield return new ErrorItem(code, msg);
        }
    }

    private static IEnumerable<ObservationItem> ParseObservations(XContainer result)
    {
        var obs = result.Descendants().FirstOrDefault(e => e.Name.LocalName == "Observaciones");
        if (obs is null) yield break;

        foreach (var o in obs.Descendants().Where(e => e.Name.LocalName == "Obs"))
        {
            var codeText = o.Descendants().FirstOrDefault(e => e.Name.LocalName == "Code")?.Value;
            var msg = o.Descendants().FirstOrDefault(e => e.Name.LocalName == "Msg")?.Value ?? "";
            if (int.TryParse(codeText, out var code))
                yield return new ObservationItem(code, msg);
        }
    }

    private static string FormatoImporte(decimal importe) => importe.ToString("F2", CultureInfo.InvariantCulture);
}
