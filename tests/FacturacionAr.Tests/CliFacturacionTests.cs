using System.Security.Cryptography.X509Certificates;

namespace FacturacionAr.Tests;

public class CliFacturacionTests : IDisposable
{
    private readonly X509Certificate2 _cert = TestCertificates.CreateSelfSigned();

    public void Dispose() => _cert.Dispose();

    [Fact]
    public async Task SolicitarCaeAsync_Reutiliza_Token_Sin_Llamar_Dos_Veces_A_WSAA()
    {
        var wsaaCalls = 0;
        var transport = new FakeTransport((url, action, body) =>
        {
            if (action.Contains("loginCms", StringComparison.OrdinalIgnoreCase))
            {
                wsaaCalls++;
                return LoginTicketResponseXml();
            }

            return FecaeOkResponseXml();
        });

        var cli = new CliFacturacion(transport, _cert, "http://mock/wsaa", "http://mock/wsfe", 20123456780);

        var comp = new Comprobante(
            Tipo: TipoComprobante.FacturaC,
            PuntoVenta: 1,
            Numero: 1,
            Fecha: DateTime.Today,
            ImporteTotal: 100m,
            ImporteNeto: 100m);

        await cli.SolicitarCaeAsync(comp);
        await cli.SolicitarCaeAsync(comp);

        Assert.Equal(1, wsaaCalls);
    }

    private static string LoginTicketResponseXml()
    {
        var ticket = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<loginTicketResponse version=""1.0"">
  <header>
    <source>CN=mock</source>
    <destination>CN=cliente</destination>
    <uniqueId>1</uniqueId>
    <generationTime>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:sszzz}</generationTime>
    <expirationTime>{DateTime.UtcNow.AddHours(12):yyyy-MM-ddTHH:mm:sszzz}</expirationTime>
  </header>
  <credentials><token>TOKEN</token><sign>SIGN</sign></credentials>
</loginTicketResponse>";

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <wsaa:loginCmsResponse xmlns:wsaa="http://wsaa.view.sua.dvadac.desein.afip.gov">
                  <wsaa:loginCmsReturn>{System.Net.WebUtility.HtmlEncode(ticket)}</wsaa:loginCmsReturn>
                </wsaa:loginCmsResponse>
              </soap:Body>
            </soap:Envelope>
            """;
    }

    private static string FecaeOkResponseXml() => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <FECAESolicitarResponse xmlns=""http://ar.gov.afip.dif.FEV1/"">
      <FECAESolicitarResult>
        <FeCabResp><Resultado>A</Resultado></FeCabResp>
        <FeDetResp>
          <FECAEDetResponse>
            <Resultado>A</Resultado>
            <CAE>12345678901234</CAE>
            <CAEFchVto>20251231</CAEFchVto>
          </FECAEDetResponse>
        </FeDetResp>
      </FECAESolicitarResult>
    </FECAESolicitarResponse>
  </soap:Body>
</soap:Envelope>";
}
