using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FacturacionAr.Mock;

/// <summary>
/// Servidor HTTP local que simula los endpoints WSAA (LoginCms) y WSFEv1
/// de AFIP/ARCA. Sirve para tests y para el ejemplo de consola sin tocar
/// la red real ni exponer credenciales.
/// </summary>
public sealed class AfipMockServer : IAsyncDisposable
{
    private WebApplication? _app;

    public Uri BaseAddress { get; private set; } = null!;

    public async Task<Uri> StartAsync(CancellationToken ct = default)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();

        app.MapPost("/ws/services/LoginCms", LoginCmsHandler);
        app.MapPost("/wsfev1/service.asmx", Wsfev1Handler);

        await app.StartAsync(ct);

        var server = app.Services.GetRequiredService<IServer>();
        var feature = server.Features.Get<IServerAddressesFeature>()!;
        BaseAddress = new Uri(feature.Addresses.First());

        _app = app;
        return BaseAddress;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_app is not null)
            await _app.StopAsync(ct);
    }

    private static async Task LoginCmsHandler(HttpContext ctx)
    {
        ctx.Response.ContentType = "text/xml; charset=utf-8";

        var body = await ReadBodyAsync(ctx);
        var doc = XDocument.Parse(body);
        var in0 = doc.Descendants()
            .First(e => e.Name.LocalName == "in0")
            .Value;

        if (!TryVerifyCms(in0, out var tra, out var error))
        {
            await ctx.Response.WriteAsync(SoapFault(error), Encoding.UTF8);
            return;
        }

        var service = ExtractService(tra);
        if (service != "wsfe")
        {
            await ctx.Response.WriteAsync(SoapFault("El servicio solicitado no está habilitado en el mock."), Encoding.UTF8);
            return;
        }

        var ticket = BuildLoginTicketResponse();
        var responseBody = $@"<wsaa:loginCmsResponse xmlns:wsaa=""http://wsaa.view.sua.dvadac.desein.afip.gov"">
  <wsaa:loginCmsReturn>{EncodeXml(ticket)}</wsaa:loginCmsReturn>
</wsaa:loginCmsResponse>";

        await ctx.Response.WriteAsync(SoapEnvelope(responseBody), Encoding.UTF8);
    }

    private static async Task Wsfev1Handler(HttpContext ctx)
    {
        ctx.Response.ContentType = "text/xml; charset=utf-8";

        var body = await ReadBodyAsync(ctx);
        var action = ctx.Request.Headers["SOAPAction"].FirstOrDefault()?.Trim('"') ?? "";

        if (action.Contains("FECAESolicitar", StringComparison.OrdinalIgnoreCase))
        {
            await ctx.Response.WriteAsync(FecaeSolicitarResponse(body), Encoding.UTF8);
            return;
        }

        if (action.Contains("FECompUltimoAutorizado", StringComparison.OrdinalIgnoreCase))
        {
            await ctx.Response.WriteAsync(FeCompUltimoAutorizadoResponse(body), Encoding.UTF8);
            return;
        }

        await ctx.Response.WriteAsync(SoapFault($"Acción SOAP no soportada: {action}"), Encoding.UTF8);
    }

    private static string FecaeSolicitarResponse(string requestXml)
    {
        var doc = XDocument.Parse(requestXml);
        var body = doc.Descendants().First(e => e.Name.LocalName == "Body");
        var request = body.Descendants().First(e => e.Name.LocalName == "FECAESolicitar");

        var cuitText = request.Descendants().First(e => e.Name.LocalName == "Cuit").Value;
        if (!long.TryParse(cuitText, out var cuit) || cuit <= 0)
        {
            return BuildFecaeResult("R", null, null,
                [new Error(10013, "El CUIT representado no es válido.")]);
        }

        var tipo = request.Descendants().First(e => e.Name.LocalName == "CbteTipo").Value;
        var ptoVta = request.Descendants().First(e => e.Name.LocalName == "PtoVta").Value;
        var numero = request.Descendants().First(e => e.Name.LocalName == "CbteDesde").Value;

        var cae = GenerarCaeFicticio();
        var vencimiento = DateTime.UtcNow.AddDays(10).ToString("yyyyMMdd");

        var observaciones = new List<Observation>();
        if (int.TryParse(tipo, out var tipoInt) && tipoInt == (int)TipoComprobante.FacturaC)
        {
            observaciones.Add(new Observation(100, "Mock: factura C emitida en modo simulado."));
        }

        return BuildFecaeResult("A", cae, vencimiento, null, observaciones);
    }

    private static string FeCompUltimoAutorizadoResponse(string requestXml)
    {
        var doc = XDocument.Parse(requestXml);
        var body = doc.Descendants().First(e => e.Name.LocalName == "Body");
        var request = body.Descendants().First(e => e.Name.LocalName == "FECompUltimoAutorizado");

        var tipo = request.Descendants().First(e => e.Name.LocalName == "CbteTipo").Value;
        var ptoVta = request.Descendants().First(e => e.Name.LocalName == "PtoVta").Value;

        var inner = $"""
            <FECompUltimoAutorizadoResult xmlns="http://ar.gov.afip.dif.FEV1/">
              <PtoVta>{ptoVta}</PtoVta>
              <CbteTipo>{tipo}</CbteTipo>
              <CbteNro>5</CbteNro>
            </FECompUltimoAutorizadoResult>
            """;

        return SoapEnvelope(inner);
    }

    private static string BuildFecaeResult(
        string resultado,
        string? cae,
        string? vencimiento,
        List<Error>? errores = null,
        List<Observation>? observaciones = null)
    {
        var obsXml = "";
        if (observaciones is { Count: > 0 })
        {
            var items = string.Join("\n", observaciones.Select(o => $"<Obs><Code>{o.Code}</Code><Msg>{EncodeXml(o.Message)}</Msg></Obs>"));
            obsXml = $"<Observaciones>{items}</Observaciones>";
        }

        var errXml = "";
        if (errores is { Count: > 0 })
        {
            var items = string.Join("\n", errores.Select(e => $"<Err><Code>{e.Code}</Code><Msg>{EncodeXml(e.Message)}</Msg></Err>"));
            errXml = $"<Errors>{items}</Errors>";
        }

        var inner = $"""
            <FECAESolicitarResult xmlns="http://ar.gov.afip.dif.FEV1/">
              <FeCabResp>
                <Resultado>{resultado}</Resultado>
                <FchProceso>{DateTime.UtcNow:yyyyMMddHHmmss}</FchProceso>
              </FeCabResp>
              <FeDetResp>
                <FECAEDetResponse>
                  <Resultado>{resultado}</Resultado>
                  <CAE>{cae ?? string.Empty}</CAE>
                  <CAEFchVto>{vencimiento ?? string.Empty}</CAEFchVto>
                  {obsXml}
                </FECAEDetResponse>
              </FeDetResp>
              {errXml}
            </FECAESolicitarResult>
            """;

        return SoapEnvelope(inner);
    }

    private static string BuildLoginTicketResponse()
    {
        var expiration = DateTime.UtcNow.AddHours(12);
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <loginTicketResponse version="1.0">
              <header>
                <source>CN=facturacion-ar-mock</source>
                <destination>CN=cliente-de-prueba</destination>
                <uniqueId>12345</uniqueId>
                <generationTime>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:sszzz}</generationTime>
                <expirationTime>{expiration:yyyy-MM-ddTHH:mm:sszzz}</expirationTime>
              </header>
              <credentials>
                <token>TOKEN_FICTICIO_{Guid.NewGuid():N}</token>
                <sign>SIGN_FICTICIO_{Guid.NewGuid():N}</sign>
              </credentials>
            </loginTicketResponse>
            """;
    }

    private static bool TryVerifyCms(string cmsBase64, out string tra, out string error)
    {
        tra = "";
        error = "";
        try
        {
            var bytes = Convert.FromBase64String(cmsBase64);
            var signedCms = new SignedCms();
            signedCms.Decode(bytes);
            signedCms.CheckSignature(true);
            tra = Encoding.UTF8.GetString(signedCms.ContentInfo.Content);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Firma CMS inválida: {ex.Message}";
            return false;
        }
    }

    private static string ExtractService(string traXml)
    {
        var doc = XDocument.Parse(traXml);
        return doc.Descendants().First(e => e.Name.LocalName == "service").Value;
    }

    private static string GenerarCaeFicticio() => $"123456{DateTime.UtcNow:HHmmss}";

    private static string SoapEnvelope(string bodyXml)
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>{bodyXml}</soap:Body>
            </soap:Envelope>
            """;
    }

    private static string SoapFault(string message)
    {
        return SoapEnvelope($"""
            <soap:Fault>
              <faultcode>soap:Server</faultcode>
              <faultstring>{EncodeXml(message)}</faultstring>
            </soap:Fault>
            """);
    }

    private static string EncodeXml(string text)
    {
        return text.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;")
                   .Replace("'", "&apos;");
    }

    private static async Task<string> ReadBodyAsync(HttpContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync(ctx.RequestAborted);
    }

    private sealed record Error(int Code, string Message);
    private sealed record Observation(int Code, string Message);
}
