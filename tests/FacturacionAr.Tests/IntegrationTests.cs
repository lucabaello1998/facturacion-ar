using System.Security.Cryptography.X509Certificates;
using FacturacionAr.Mock;

namespace FacturacionAr.Tests;

public class IntegrationTests : IAsyncLifetime
{
    private readonly AfipMockServer _mock = new();
    private readonly X509Certificate2 _cert = TestCertificates.CreateSelfSigned();
    private string _wsaaUrl = null!;
    private string _wsfeUrl = null!;

    public async Task InitializeAsync()
    {
        var baseUri = await _mock.StartAsync();
        _wsaaUrl = new Uri(baseUri, "/ws/services/LoginCms").ToString();
        _wsfeUrl = new Uri(baseUri, "/wsfev1/service.asmx").ToString();
    }

    public async Task DisposeAsync()
    {
        await _mock.DisposeAsync();
        _cert.Dispose();
    }

    [Fact]
    public async Task EndToEnd_FacturaC_Obtiene_CAE_Ficticio()
    {
        using var transport = new HttpWsnTransport();
        var cli = new CliFacturacion(transport, _cert, _wsaaUrl, _wsfeUrl, 20123456780);

        var comp = new Comprobante(
            Tipo: TipoComprobante.FacturaC,
            PuntoVenta: 1,
            Numero: 2,
            Fecha: DateTime.Today,
            ImporteTotal: 1000m,
            ImporteNeto: 1000m);

        var resp = await cli.SolicitarCaeAsync(comp);

        Assert.True(resp.Aprobado);
        Assert.Equal("A", resp.Resultado);
        Assert.False(string.IsNullOrEmpty(resp.CAE));
        Assert.NotNull(resp.CAEVencimiento);
    }

    [Fact]
    public async Task EndToEnd_FacturaA_Obtiene_CAE_Ficticio()
    {
        using var transport = new HttpWsnTransport();
        var cli = new CliFacturacion(transport, _cert, _wsaaUrl, _wsfeUrl, 20123456780);

        var comp = new Comprobante(
            Tipo: TipoComprobante.FacturaA,
            PuntoVenta: 1,
            Numero: 3,
            Fecha: DateTime.Today,
            ImporteTotal: 1210m,
            ImporteNeto: 1000m,
            ReceptorCuit: 30123456789,
            AlicuotasIva: [new AlicuotaIva(5, 1000m, 210m)]);

        var resp = await cli.SolicitarCaeAsync(comp);

        Assert.True(resp.Aprobado);
    }

    [Fact]
    public async Task EndToEnd_UltimoAutorizado_Retorna_Numero()
    {
        using var transport = new HttpWsnTransport();
        var cli = new CliFacturacion(transport, _cert, _wsaaUrl, _wsfeUrl, 20123456780);

        var ultimo = await cli.ObtenerUltimoAutorizadoAsync(TipoComprobante.FacturaC, 1);

        Assert.True(ultimo > 0);
    }
}
