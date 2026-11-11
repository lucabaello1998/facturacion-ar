using System.Xml;
using System.Xml.Linq;
using Xunit;

namespace FacturacionAr.Tests;

/// <summary>
/// Robustez frente a respuestas hostiles del servicio: un XML con DTD y entidad
/// externa no puede terminar leyendo un archivo local (XXE).
/// </summary>
public class HardeningTests
{
    [Fact]
    public void Un_XML_con_entidad_externa_no_filtra_archivos_locales()
    {
        const string hostil = """
            <?xml version="1.0"?>
            <!DOCTYPE foo [ <!ENTITY xxe SYSTEM "file:///c:/windows/win.ini"> ]>
            <loginCmsResponse><loginCmsReturn>&xxe;</loginCmsReturn></loginCmsResponse>
            """;

        string? valor = null;
        try
        {
            var documento = XDocument.Parse(hostil);
            valor = documento.Descendants("loginCmsReturn").FirstOrDefault()?.Value;
        }
        catch (XmlException)
        {
            // Prohibir el DTD también es una respuesta segura.
            return;
        }

        Assert.DoesNotContain("[fonts]", valor ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[extensions]", valor ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void El_transporte_usa_timeout_por_defecto()
    {
        using var transporte = new HttpWsnTransport();
        var campo = typeof(HttpWsnTransport).GetField(
            "_httpClient",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cliente = (HttpClient?)campo?.GetValue(transporte);

        Assert.NotNull(cliente);
        Assert.True(cliente!.Timeout <= TimeSpan.FromSeconds(60), $"timeout actual: {cliente.Timeout}");
    }
}
