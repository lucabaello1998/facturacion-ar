using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FacturacionAr;
using FacturacionAr.Mock;

if (args.Contains("--mock"))
{
    await RunMockScenarioAsync();
    return 0;
}

Console.WriteLine("Uso: dotnet run --project src/FacturacionAr.Cli -- --mock");
return 1;

static async Task RunMockScenarioAsync()
{
    using var cert = GenerarCertificadoAutofirmado();
    await using var mock = new AfipMockServer();
    var baseUri = await mock.StartAsync();

    var wsaaUrl = new Uri(baseUri, "/ws/services/LoginCms").ToString();
    var wsfeUrl = new Uri(baseUri, "/wsfev1/service.asmx").ToString();

    using var transport = new HttpWsnTransport();
    var cli = new CliFacturacion(transport, cert, wsaaUrl, wsfeUrl, cuit: 20123456780);

    var comprobante = new Comprobante(
        Tipo: TipoComprobante.FacturaC,
        PuntoVenta: 1,
        Numero: 2,
        Fecha: DateTime.Today,
        ImporteTotal: 1210.00m,
        ImporteNeto: 1000.00m,
        ImporteExentoIva: 210.00m);

    Console.WriteLine("Facturación electrónica Argentina - modo simulado (mock)");
    Console.WriteLine($"Mock URL: {baseUri}");

    var respuesta = await cli.SolicitarCaeAsync(comprobante);

    if (respuesta.Aprobado)
    {
        Console.WriteLine($"Resultado: {respuesta.Resultado}");
        Console.WriteLine($"CAE ficticio: {respuesta.CAE}");
        Console.WriteLine($"Vencimiento CAE: {respuesta.CAEVencimiento:yyyy-MM-dd}");
    }
    else
    {
        Console.WriteLine($"Resultado: {respuesta.Resultado}");
        foreach (var e in respuesta.Errores)
            Console.WriteLine($"Error {e.Code}: {e.Message}");
        foreach (var o in respuesta.Observaciones)
            Console.WriteLine($"Observación {o.Code}: {o.Message}");
        Environment.ExitCode = 2;
    }

    foreach (var o in respuesta.Observaciones)
        Console.WriteLine($"Observación {o.Code}: {o.Message}");
}

static X509Certificate2 GenerarCertificadoAutofirmado()
{
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest(
        "CN=facturacion-ar-test, O=Ejemplo, C=AR",
        rsa,
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1);

    request.CertificateExtensions.Add(
        new X509BasicConstraintsExtension(true, false, 0, true));
    request.CertificateExtensions.Add(
        new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

    var cert = request.CreateSelfSigned(
        DateTimeOffset.UtcNow.AddDays(-1),
        DateTimeOffset.UtcNow.AddDays(1));

    return new X509Certificate2(cert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
}
