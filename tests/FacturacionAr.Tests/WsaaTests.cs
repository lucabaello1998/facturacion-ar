using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;

namespace FacturacionAr.Tests;

public class WsaaTests : IDisposable
{
    private readonly X509Certificate2 _cert = TestCertificates.CreateSelfSigned();

    public void Dispose() => _cert.Dispose();

    [Fact]
    public void BuildTra_Contiene_Servicio_Y_Header()
    {
        var tra = WsaaClient.BuildTra("wsfe");

        Assert.Contains("<service>wsfe</service>", tra);
        Assert.Contains("<uniqueId>", tra);
        Assert.Contains("<generationTime>", tra);
        Assert.Contains("<expirationTime>", tra);
    }

    [Fact]
    public void BuildTra_Tiempos_Tienen_Rango_Coherente()
    {
        var tra = WsaaClient.BuildTra("wsfe");
        var doc = XDocument.Parse(tra);
        var header = doc.Descendants().First(e => e.Name.LocalName == "header");
        var gen = DateTime.Parse(header.Descendants().First(e => e.Name.LocalName == "generationTime").Value);
        var exp = DateTime.Parse(header.Descendants().First(e => e.Name.LocalName == "expirationTime").Value);

        Assert.True(exp > gen);
        Assert.True(exp - gen <= TimeSpan.FromMinutes(25));
    }

    [Fact]
    public void SignCms_Produce_Base64_No_Vacio()
    {
        var tra = WsaaClient.BuildTra("wsfe");
        var cms = WsaaClient.SignCms(tra, _cert);

        Assert.False(string.IsNullOrWhiteSpace(cms));
        var bytes = Convert.FromBase64String(cms);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void SignCms_Cms_Puede_Verificarse()
    {
        var tra = WsaaClient.BuildTra("wsfe");
        var cmsBase64 = WsaaClient.SignCms(tra, _cert);

        var signedCms = new SignedCms();
        signedCms.Decode(Convert.FromBase64String(cmsBase64));
        signedCms.CheckSignature(true);

        var contenido = Encoding.UTF8.GetString(signedCms.ContentInfo.Content);
        Assert.Equal(tra, contenido);
    }

    [Fact]
    public async Task AuthenticateAsync_Retorna_Credenciales_Desde_Transporte_Falso()
    {
        var transport = new FakeTransport(LoginTicketResponseXml());
        var client = new WsaaClient(transport, _cert);

        var creds = await client.AuthenticateAsync("http://mock/wsaa", CancellationToken.None);

        Assert.NotNull(creds);
        Assert.NotEmpty(creds.Token);
        Assert.NotEmpty(creds.Sign);
        Assert.True(creds.Expiration > DateTime.UtcNow);
    }

    [Fact]
    public async Task AuthenticateAsync_Cachea_Credenciales()
    {
        var calls = 0;
        var transport = new FakeTransport((url, action, body) =>
        {
            calls++;
            return LoginTicketResponseXml();
        });
        var client = new WsaaClient(transport, _cert, renewalMargin: TimeSpan.FromMinutes(5));

        var a = await client.AuthenticateAsync("http://mock/wsaa", CancellationToken.None);
        var b = await client.AuthenticateAsync("http://mock/wsaa", CancellationToken.None);

        Assert.Equal(a.Token, b.Token);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task AuthenticateAsync_Renueva_Cuando_Queda_Poco_Tiempo()
    {
        var calls = 0;
        var transport = new FakeTransport((url, action, body) =>
        {
            calls++;
            return LoginTicketResponseXml(expiration: DateTime.UtcNow.AddMinutes(2));
        });
        var client = new WsaaClient(transport, _cert, renewalMargin: TimeSpan.FromMinutes(5));

        await client.AuthenticateAsync("http://mock/wsaa", CancellationToken.None);
        await client.AuthenticateAsync("http://mock/wsaa", CancellationToken.None);

        Assert.Equal(2, calls);
    }

    private static string LoginTicketResponseXml(DateTime? expiration = null)
    {
        var exp = expiration ?? DateTime.UtcNow.AddHours(12);
        var ticket = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<loginTicketResponse version=""1.0"">
  <header>
    <source>CN=mock</source>
    <destination>CN=cliente</destination>
    <uniqueId>1</uniqueId>
    <generationTime>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:sszzz}</generationTime>
    <expirationTime>{exp:yyyy-MM-ddTHH:mm:sszzz}</expirationTime>
  </header>
  <credentials>
    <token>TOKEN123</token>
    <sign>SIGN123</sign>
  </credentials>
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
}
