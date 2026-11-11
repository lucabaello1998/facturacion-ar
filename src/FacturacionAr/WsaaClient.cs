using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;

namespace FacturacionAr;

public sealed class WsaaClient
{
    private readonly IWsnTransport _transport;
    private readonly X509Certificate2 _cert;
    private readonly string _service;
    private readonly TimeSpan _renewalMargin;

    private WsaaCredentials? _cached;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public WsaaClient(
        IWsnTransport transport,
        X509Certificate2 cert,
        string service = "wsfe",
        TimeSpan? renewalMargin = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _cert = cert ?? throw new ArgumentNullException(nameof(cert));
        _service = service;
        _renewalMargin = renewalMargin ?? TimeSpan.FromMinutes(10);
    }

    public async Task<WsaaCredentials> AuthenticateAsync(string endpointUrl, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_cached is not null && DateTime.UtcNow < _cached.Expiration - _renewalMargin)
                return _cached;

            var tra = BuildTra(_service);
            var cms = SignCms(tra, _cert);
            var soap = BuildLoginCmsSoap(cms);

            const string soapAction = "http://wsaa.view.sua.dvadac.desein.afip.gov/loginCms";
            var responseXml = await _transport.PostSoapAsync(endpointUrl, soapAction, soap, ct);

            _cached = ParseLoginTicketResponse(responseXml);
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    internal static string BuildTra(string service)
    {
        var now = DateTime.UtcNow;
        var generation = now.AddMinutes(-10);
        var expiration = now.AddMinutes(10);
        var uniqueId = (int)(now.Ticks % int.MaxValue);

        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<loginTicketRequest version=""1.0"">
  <header>
    <uniqueId>{uniqueId}</uniqueId>
    <generationTime>{generation:yyyy-MM-ddTHH:mm:sszzz}</generationTime>
    <expirationTime>{expiration:yyyy-MM-ddTHH:mm:sszzz}</expirationTime>
  </header>
  <service>{service}</service>
</loginTicketRequest>";
    }

    internal static string SignCms(string tra, X509Certificate2 cert)
    {
        var contentBytes = Encoding.UTF8.GetBytes(tra);
        var contentInfo = new ContentInfo(contentBytes);
        var signedCms = new SignedCms(contentInfo, false);

        var signer = new CmsSigner(cert)
        {
            IncludeOption = X509IncludeOption.EndCertOnly
        };

        signedCms.ComputeSignature(signer);
        var encoded = signedCms.Encode();
        return Convert.ToBase64String(encoded);
    }

    internal static string BuildLoginCmsSoap(string cmsBase64)
    {
        XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
        XNamespace wsaa = "http://wsaa.view.sua.dvadac.desein.afip.gov";

        var envelope = new XElement(soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", soap.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "wsaa", wsaa.NamespaceName),
            new XElement(soap + "Body",
                new XElement(wsaa + "loginCms",
                    new XElement(wsaa + "in0", cmsBase64))));

        return envelope.ToString();
    }

    internal static WsaaCredentials ParseLoginTicketResponse(string responseXml)
    {
        var doc = XDocument.Parse(responseXml);
        var body = doc.Descendants().First(e => e.Name.LocalName == "Body");
        var returnElement = body.Descendants().First(e => e.Name.LocalName == "loginCmsReturn");
        var ticketXml = returnElement.Value;

        var ticket = XDocument.Parse(ticketXml);
        var credentials = ticket.Descendants().First(e => e.Name.LocalName == "credentials");
        var token = credentials.Descendants().First(e => e.Name.LocalName == "token").Value;
        var sign = credentials.Descendants().First(e => e.Name.LocalName == "sign").Value;
        var expirationText = ticket.Descendants().First(e => e.Name.LocalName == "expirationTime").Value;
        var expiration = DateTime.Parse(expirationText);

        return new WsaaCredentials(token, sign, expiration.ToUniversalTime());
    }
}
