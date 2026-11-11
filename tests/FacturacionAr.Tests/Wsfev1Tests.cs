using System.Xml.Linq;

namespace FacturacionAr.Tests;

public class Wsfev1Tests
{
    private static readonly WsaaCredentials Creds = new("TOKEN", "SIGN", DateTime.UtcNow.AddHours(1));

    [Fact]
    public void BuildFecaeSolicitarSoap_Contiene_Auth_Y_Datos_De_Comprobante()
    {
        var comp = FacturaAValida();
        var soap = Wsfev1Client.BuildFecaeSolicitarSoap(Creds, 20123456780, comp);

        Assert.Contains("<fe:Auth>", soap);
        Assert.Contains("<fe:Token>TOKEN</fe:Token>", soap);
        Assert.Contains("<fe:Sign>SIGN</fe:Sign>", soap);
        Assert.Contains("<fe:Cuit>20123456780</fe:Cuit>", soap);
        Assert.Contains("<fe:CbteTipo>1</fe:CbteTipo>", soap);
        Assert.Contains("<fe:PtoVta>1</fe:PtoVta>", soap);
        Assert.Contains("<fe:CbteDesde>2</fe:CbteDesde>", soap);
        Assert.Contains("<fe:AlicIva>", soap);
    }

    [Fact]
    public void BuildFecaeSolicitarSoap_FacturaC_Usa_DocTipo_Consumidor_Final()
    {
        var comp = FacturaCValida();
        var soap = Wsfev1Client.BuildFecaeSolicitarSoap(Creds, 20123456780, comp);

        Assert.Contains("<fe:DocTipo>99</fe:DocTipo>", soap);
        Assert.Contains("<fe:DocNro>0</fe:DocNro>", soap);
        Assert.DoesNotContain("<fe:AlicIva>", soap);
    }

    [Fact]
    public void ParseOkResponse_Retorna_CAE_Y_Vencimiento()
    {
        var xml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <FECAESolicitarResponse xmlns=""http://ar.gov.afip.dif.FEV1/"">
      <FECAESolicitarResult>
        <FeCabResp><Resultado>A</Resultado></FeCabResp>
        <FeDetResp>
          <FECAEDetResponse>
            <Resultado>A</Resultado>
            <CAE>12345678901234</CAE>
            <CAEFchVto>{DateTime.UtcNow.AddDays(10):yyyyMMdd}</CAEFchVto>
          </FECAEDetResponse>
        </FeDetResp>
      </FECAESolicitarResult>
    </FECAESolicitarResponse>
  </soap:Body>
</soap:Envelope>";

        var response = Wsfev1Client.ParseFecaeSolicitarResponse(xml);

        Assert.True(response.Aprobado);
        Assert.Equal("A", response.Resultado);
        Assert.Equal("12345678901234", response.CAE);
        Assert.NotNull(response.CAEVencimiento);
    }

    [Fact]
    public void ParseErrorResponse_Retorna_Errores()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <FECAESolicitarResponse xmlns=""http://ar.gov.afip.dif.FEV1/"">
      <FECAESolicitarResult>
        <FeCabResp><Resultado>R</Resultado></FeCabResp>
        <FeDetResp><FECAEDetResponse><Resultado>R</Resultado></FECAEDetResponse></FeDetResp>
        <Errors><Err><Code>10013</Code><Msg>CUIT invalido</Msg></Err></Errors>
      </FECAESolicitarResult>
    </FECAESolicitarResponse>
  </soap:Body>
</soap:Envelope>";

        var response = Wsfev1Client.ParseFecaeSolicitarResponse(xml);

        Assert.False(response.Aprobado);
        Assert.Single(response.Errores);
        Assert.Equal(10013, response.Errores[0].Code);
    }

    [Fact]
    public void ParseObservations_Retorna_Observaciones()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
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
            <Observaciones><Obs><Code>100</Code><Msg>Mensaje informativo</Msg></Obs></Observaciones>
          </FECAEDetResponse>
        </FeDetResp>
      </FECAESolicitarResult>
    </FECAESolicitarResponse>
  </soap:Body>
</soap:Envelope>";

        var response = Wsfev1Client.ParseFecaeSolicitarResponse(xml);

        Assert.Single(response.Observaciones);
        Assert.Equal(100, response.Observaciones[0].Code);
    }

    [Fact]
    public void ParseFeCompUltimoAutorizado_Retorna_Numero()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <FECompUltimoAutorizadoResponse xmlns=""http://ar.gov.afip.dif.FEV1/"">
      <FECompUltimoAutorizadoResult>
        <PtoVta>1</PtoVta>
        <CbteTipo>11</CbteTipo>
        <CbteNro>42</CbteNro>
      </FECompUltimoAutorizadoResult>
    </FECompUltimoAutorizadoResponse>
  </soap:Body>
</soap:Envelope>";

        var numero = Wsfev1Client.ParseFeCompUltimoAutorizadoResponse(xml);

        Assert.Equal(42, numero);
    }

    private static Comprobante FacturaAValida() => new(
        Tipo: TipoComprobante.FacturaA,
        PuntoVenta: 1,
        Numero: 2,
        Fecha: DateTime.Today,
        ImporteTotal: 1210.00m,
        ImporteNeto: 1000.00m,
        ReceptorCuit: 30123456789,
        AlicuotasIva: [new AlicuotaIva(5, 1000m, 210m)]);

    private static Comprobante FacturaCValida() => new(
        Tipo: TipoComprobante.FacturaC,
        PuntoVenta: 1,
        Numero: 2,
        Fecha: DateTime.Today,
        ImporteTotal: 1000.00m,
        ImporteNeto: 1000.00m);
}
