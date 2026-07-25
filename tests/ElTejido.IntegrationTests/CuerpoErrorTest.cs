namespace ElTejido.IntegrationTests;

// P-17 (API-001): DTOs compartidos para deserializar el cuerpo de error uniforme (04 §3) en las
// pruebas de integracion de rutas con resultados directos (webhook, preparacion, filtro de
// diagnostico, jobs admin). Verifican codigo estable + mensaje seguro + correlationId del cuerpo.
internal sealed record CuerpoErrorTest(ErrorTest Error);

internal sealed record ErrorTest(
    string Code,
    string Message,
    IReadOnlyList<DetalleErrorTest>? Details,
    string CorrelationId);

internal sealed record DetalleErrorTest(string? Field, string Issue);
