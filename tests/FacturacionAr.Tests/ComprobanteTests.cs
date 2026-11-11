namespace FacturacionAr.Tests;

public class ComprobanteTests
{
    [Fact]
    public void FacturaA_Valida_Cuando_IVA_Coincide()
    {
        var comp = new Comprobante(
            Tipo: TipoComprobante.FacturaA,
            PuntoVenta: 1,
            Numero: 1,
            Fecha: DateTime.Today,
            ImporteTotal: 1210.00m,
            ImporteNeto: 1000.00m,
            ReceptorCuit: 30123456789,
            AlicuotasIva: [new AlicuotaIva(5, 1000m, 210m)]);

        comp.Validar();
    }

    [Fact]
    public void FacturaC_Valida_Sin_Alicuotas()
    {
        var comp = new Comprobante(
            Tipo: TipoComprobante.FacturaC,
            PuntoVenta: 1,
            Numero: 1,
            Fecha: DateTime.Today,
            ImporteTotal: 1500.00m,
            ImporteNeto: 1500.00m);

        comp.Validar();
    }

    [Fact]
    public void ImporteTotal_Negativo_Lanza_Excepcion()
    {
        var comp = new Comprobante(
            Tipo: TipoComprobante.FacturaC,
            PuntoVenta: 1,
            Numero: 1,
            Fecha: DateTime.Today,
            ImporteTotal: -1m,
            ImporteNeto: -1m);

        Assert.Throws<ArgumentException>(() => comp.Validar());
    }

    [Fact]
    public void IVA_Incoherente_Lanza_Excepcion()
    {
        var comp = new Comprobante(
            Tipo: TipoComprobante.FacturaA,
            PuntoVenta: 1,
            Numero: 1,
            Fecha: DateTime.Today,
            ImporteTotal: 1210.00m,
            ImporteNeto: 1000.00m,
            ReceptorCuit: 30123456789,
            AlicuotasIva: [new AlicuotaIva(5, 1000m, 100m)]);

        Assert.Throws<InvalidOperationException>(() => comp.Validar());
    }

    [Fact]
    public void PuntoVenta_Fuera_De_Rango_Lanza_Excepcion()
    {
        var comp = new Comprobante(
            Tipo: TipoComprobante.FacturaC,
            PuntoVenta: 99999,
            Numero: 1,
            Fecha: DateTime.Today,
            ImporteTotal: 100m,
            ImporteNeto: 100m);

        Assert.Throws<ArgumentException>(() => comp.Validar());
    }

    [Fact]
    public void Numero_Cero_Lanza_Excepcion()
    {
        var comp = new Comprobante(
            Tipo: TipoComprobante.FacturaC,
            PuntoVenta: 1,
            Numero: 0,
            Fecha: DateTime.Today,
            ImporteTotal: 100m,
            ImporteNeto: 100m);

        Assert.Throws<ArgumentException>(() => comp.Validar());
    }

    [Fact]
    public void Tipo_No_Definido_Lanza_Excepcion()
    {
        var comp = new Comprobante(
            Tipo: (TipoComprobante)999,
            PuntoVenta: 1,
            Numero: 1,
            Fecha: DateTime.Today,
            ImporteTotal: 100m,
            ImporteNeto: 100m);

        Assert.Throws<ArgumentException>(() => comp.Validar());
    }

    [Fact]
    public void FacturaA_ReceptorCuit_Faltante_Lanza_Excepcion()
    {
        var comp = new Comprobante(
            Tipo: TipoComprobante.FacturaA,
            PuntoVenta: 1,
            Numero: 1,
            Fecha: DateTime.Today,
            ImporteTotal: 1210.00m,
            ImporteNeto: 1000.00m,
            AlicuotasIva: [new AlicuotaIva(5, 1000m, 210m)]);

        Assert.Throws<InvalidOperationException>(() => comp.Validar());
    }

    [Fact]
    public void FacturaC_Con_Alicuotas_Lanza_Excepcion()
    {
        var comp = new Comprobante(
            Tipo: TipoComprobante.FacturaC,
            PuntoVenta: 1,
            Numero: 1,
            Fecha: DateTime.Today,
            ImporteTotal: 1000m,
            ImporteNeto: 1000m,
            AlicuotasIva: [new AlicuotaIva(5, 1000m, 0m)]);

        Assert.Throws<InvalidOperationException>(() => comp.Validar());
    }

    [Fact]
    public void Alicuota_Codigo_Invalido_Lanza_Excepcion()
    {
        var comp = new Comprobante(
            Tipo: TipoComprobante.FacturaA,
            PuntoVenta: 1,
            Numero: 1,
            Fecha: DateTime.Today,
            ImporteTotal: 1000m,
            ImporteNeto: 1000m,
            ReceptorCuit: 30123456789,
            AlicuotasIva: [new AlicuotaIva(99, 1000m, 0m)]);

        Assert.Throws<ArgumentException>(() => comp.Validar());
    }
}
