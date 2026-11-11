# FacturacionAr

Biblioteca cliente .NET 8 para facturación electrónica argentina (AFIP / ARCA).

Resuelve el armado de los mensajes SOAP de **WSAA** (autenticación con certificado,
token + sign) y **WSFEv1** (`FECAESolicitar`, `FECompUltimoAutorizado`) para que no
tengas que reescribir XML a mano en cada integración.

Incluye un **servidor mock** que simula los endpoints de AFIP, así se puede
desarrollar y testear sin certificado real y sin tocar producción.

> **Todos los datos de ejemplo son ficticios.** No se usan CUITs, puntos de venta
> ni comprobantes reales. Este repo **no contiene credenciales**.

## Flujo

```
+-------------+   certificado   +-----------------+
|   Cliente   | --------------> | WSAA (mock/real)|
|  FacturacionAr|  TRA firmado  |  token + sign   |
+-------------+                 +-----------------+
       |                                 |
       | token/sign + factura            v
       | ---------------->  +-----------------------+
       |                    | WSFEv1 (mock/real)    |
       | <----------------  | CAE / errores         |
       |                    +-----------------------+
```

1. Se arma un `loginTicketRequest` (TRA).
2. Se firma con el certificado (`SignedCms` / PKCS#7).
3. Se llama a `LoginCms` y se cachean `token` + `sign` hasta su vencimiento.
4. Con esas credenciales se envía el comprobante a `FECAESolicitar`.

## Ejemplo de uso

```csharp
using var cert = new X509Certificate2("mi_certificado.pfx", "clave");
using var transport = new HttpWsnTransport();

var cli = new CliFacturacion(
    transport,
    cert,
    wsaaUrl:  "https://wsaahomo.afip.gov.ar/ws/services/LoginCms",
    wsfeUrl:  "https://wswhomo.afip.gov.ar/wsfev1/service.asmx",
    cuit:     20999999999);

var factura = new Comprobante(
    Tipo: TipoComprobante.FacturaC,
    PuntoVenta: 1,
    Numero: 2,
    Fecha: DateTime.Today,
    ImporteTotal: 1000.00m,
    ImporteNeto: 1000.00m);

var respuesta = await cli.SolicitarCaeAsync(factura);
Console.WriteLine($"CAE: {respuesta.CAE}");
```

## Cómo correr el mock

```bash
dotnet run --project src/FacturacionAr.Cli -- --mock
```

Salida real del comando:

```text
Facturación electrónica Argentina - modo simulado (mock)
Mock URL: http://127.0.0.1:49636/
Resultado: A
CAE ficticio: 123456161217
Vencimiento CAE: 2026-11-21
Observación 100: Mock: factura C emitida en modo simulado.
```

## Tests

```bash
dotnet test
```

El proyecto de tests genera certificados autofirmados en memoria; no se
commitean archivos `.pfx`.

## Qué falta para ir a producción

Este repo está preparado para integrar, pero **no se puede probar contra AFIP
real sin completar primero**:

1. **Certificado X.509 emitido por AFIP/ARCA** (CSR + clave privada).
2. **Delegación de servicios** en el administrador de relaciones de AFIP para el
   CUIT del contribuyente (servicio `wsfe`).
3. **Punto de venta habilitado** en el portal de AFIP/ARCA para el CUIT real.
4. Cambiar las URLs a **homologación** y luego a **producción**.

No prometemos que "funcione contra AFIP": lo verificado acá es el protocolo,
la firma CMS/PKCS#7 y los esquemas de mensaje contra el simulador local.

## Límites

- Esta versión cubre principalmente **Facturas A y C** y las operaciones
  `FECAESolicitar` / `FECompUltimoAutorizado`.
- No cubre todos los tipos de comprobante, notas de crédito/débito completas,
  ni otros webservices de AFIP/ARCA (`WSBFE`, `WSCT`, etc.).
- No es asesoría fiscal: consultá a un contador para la parametrización real
  (condición de IVA, conceptos, tributos, etc.).

## Licencia

MIT © Luca Baello
