namespace FacturacionAr;

public sealed record Comprobante(
    TipoComprobante Tipo,
    int PuntoVenta,
    long Numero,
    DateTime Fecha,
    decimal ImporteTotal,
    decimal ImporteNeto,
    decimal ImporteExentoIva = 0,
    long? ReceptorCuit = null,
    IReadOnlyList<AlicuotaIva>? AlicuotasIva = null)
{
    public void Validar()
    {
        if (!Enum.IsDefined(typeof(TipoComprobante), Tipo))
            throw new ArgumentException("El tipo de comprobante no es válido.", nameof(Tipo));

        if (PuntoVenta is <= 0 or > 9998)
            throw new ArgumentException("El punto de venta debe estar entre 1 y 9998.", nameof(PuntoVenta));

        if (Numero <= 0)
            throw new ArgumentException("El número de comprobante debe ser mayor a cero.", nameof(Numero));

        if (Fecha == default)
            throw new ArgumentException("La fecha de emisión es obligatoria.", nameof(Fecha));

        if (ImporteTotal <= 0)
            throw new ArgumentException("El importe total debe ser mayor a cero.", nameof(ImporteTotal));

        if (ImporteNeto < 0)
            throw new ArgumentException("El importe neto no puede ser negativo.", nameof(ImporteNeto));

        if (ImporteExentoIva < 0)
            throw new ArgumentException("El importe exento no puede ser negativo.", nameof(ImporteExentoIva));

        var alicuotas = AlicuotasIva ?? Array.Empty<AlicuotaIva>();
        foreach (var a in alicuotas)
            a.Validar();

        var ivaTotal = alicuotas.Sum(a => a.Importe);

        if (Tipo == TipoComprobante.FacturaC || Tipo == TipoComprobante.NotaCreditoC || Tipo == TipoComprobante.NotaDebitoC)
        {
            if (alicuotas.Count != 0)
                throw new InvalidOperationException("Los comprobantes C no llevan alícuotas de IVA discriminadas.");

            if (ImporteNeto != ImporteTotal - ImporteExentoIva)
                throw new InvalidOperationException("En comprobantes C el importe neto debe coincidir con el total menos exentos.");
        }
        else
        {
            if (alicuotas.Count == 0)
                throw new InvalidOperationException("Los comprobantes A/B deben incluir alícuotas de IVA.");

            if (ivaTotal != ImporteTotal - ImporteNeto - ImporteExentoIva)
                throw new InvalidOperationException("El importe de IVA no coincide con la diferencia entre total, neto y exentos.");

            if (!ReceptorCuit.HasValue || ReceptorCuit.Value <= 0)
                throw new InvalidOperationException("Los comprobantes A requieren el CUIT del receptor.");
        }
    }
}
