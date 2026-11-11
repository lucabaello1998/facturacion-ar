using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace FacturacionAr.Tests;

public class CmsTests : IDisposable
{
    private readonly X509Certificate2 _cert = TestCertificates.CreateSelfSigned();

    public void Dispose() => _cert.Dispose();

    [Fact]
    public void Certificado_Autofirmado_Tiene_Clave_Privada()
    {
        Assert.NotNull(_cert.GetRSAPrivateKey());
    }

    [Fact]
    public void Firma_Cms_Verifica_Contenido_Y_Firma()
    {
        var contenido = "contenido de prueba";
        var cmsBase64 = WsaaClient.SignCms(contenido, _cert);

        var signedCms = new SignedCms();
        signedCms.Decode(Convert.FromBase64String(cmsBase64));
        signedCms.CheckSignature(true);

        var recuperado = Encoding.UTF8.GetString(signedCms.ContentInfo.Content);
        Assert.Equal(contenido, recuperado);
    }
}
