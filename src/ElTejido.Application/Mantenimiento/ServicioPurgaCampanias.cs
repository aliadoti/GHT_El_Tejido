using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Markdown;
using ElTejido.Application.Participantes;
using ElTejido.Application.Respuestas;
using ElTejido.Application.Seguridad;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Seguridad;

namespace ElTejido.Application.Mantenimiento;

/// <summary>
/// Implementacion de P-15. Recorre todas las campañas y, por cada una, borra en el orden seguro
/// <c>responses → conversations → participants → campaña</c> (evita huerfanos si se interrumpe:
/// re-ejecutar completa la limpieza). Al final elimina los usuarios no administrativos. Es idempotente:
/// reinvocarla sobre datos ya limpios devuelve ceros. La salida se audita en
/// <see cref="IRepositorioLogSeguridad"/> con conteos y correlationId; no registra PII.
/// </summary>
public sealed class ServicioPurgaCampanias : IServicioPurgaCampanias
{
    private readonly IRepositorioCampanias _campanias;
    private readonly IRepositorioParticipantes _participantes;
    private readonly IRepositorioConversaciones _conversaciones;
    private readonly IRepositorioRespuestas _respuestas;
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IAlmacenBlob _blob;
    private readonly IRepositorioLogSeguridad _logSeguridad;
    private readonly IProveedorCorrelacion _correlacion;
    private readonly TimeProvider _tiempo;

    public ServicioPurgaCampanias(
        IRepositorioCampanias campanias,
        IRepositorioParticipantes participantes,
        IRepositorioConversaciones conversaciones,
        IRepositorioRespuestas respuestas,
        IRepositorioUsuarios usuarios,
        IAlmacenBlob blob,
        IRepositorioLogSeguridad logSeguridad,
        IProveedorCorrelacion correlacion,
        TimeProvider tiempo)
    {
        _campanias = campanias;
        _participantes = participantes;
        _conversaciones = conversaciones;
        _respuestas = respuestas;
        _usuarios = usuarios;
        _blob = blob;
        _logSeguridad = logSeguridad;
        _correlacion = correlacion;
        _tiempo = tiempo;
    }

    public async Task<ReportePurgaCampanias> PurgarTodoAsync(CancellationToken cancellationToken)
    {
        var reporte = ReportePurgaCampanias.Vacio;

        var campanias = await _campanias.BuscarCampaniasAsync(new FiltroCampanias(), cancellationToken);
        foreach (var campania in campanias)
        {
            reporte = reporte with { Campanias = reporte.Campanias + 1 };
            reporte = await PurgarCampaniaAsync(campania.Id, reporte, cancellationToken);
            await _campanias.EliminarCampaniaAsync(campania.Id, cancellationToken);
        }

        var usuariosBorrados = await _usuarios.EliminarUsuariosNoAdministrativosAsync(cancellationToken);
        reporte = reporte with { UsuariosBorrados = usuariosBorrados };

        await AuditarAsync(reporte, cancellationToken);
        return reporte;
    }

    // Borra respuestas/evaluaciones/artefactos, luego conversaciones/mensajes, luego sus blobs (fallo
    // tolerado) y por ultimo los participantes/envios de la campania. El orden responses → conversations
    // → participants evita dejar referencias huerfanas si la operacion se interrumpe.
    private async Task<ReportePurgaCampanias> PurgarCampaniaAsync(
        string campaniaId,
        ReportePurgaCampanias acumulado,
        CancellationToken cancellationToken)
    {
        var respuestas = await _respuestas.EliminarPorUsuarioAsync(campaniaId, usuarioId: null, cancellationToken);
        var conversaciones = await _conversaciones.EliminarPorUsuarioAsync(campaniaId, usuarioId: null, cancellationToken);

        var blobsBorrados = 0;
        var blobsFallidos = 0;
        foreach (var ruta in respuestas.RutasBlob)
        {
            if (string.IsNullOrWhiteSpace(ruta))
            {
                continue;
            }

            if (await _blob.EliminarAsync(ruta, cancellationToken))
            {
                blobsBorrados++;
            }
            else
            {
                blobsFallidos++;
            }
        }

        var participantes = await _participantes.EliminarPorCampaniaAsync(campaniaId, cancellationToken);

        return acumulado with
        {
            Conversaciones = acumulado.Conversaciones + conversaciones.Conversaciones,
            Mensajes = acumulado.Mensajes + conversaciones.Mensajes,
            Respuestas = acumulado.Respuestas + respuestas.Respuestas,
            Evaluaciones = acumulado.Evaluaciones + respuestas.Evaluaciones,
            Artefactos = acumulado.Artefactos + respuestas.Artefactos,
            BlobsBorrados = acumulado.BlobsBorrados + blobsBorrados,
            BlobsFallidos = acumulado.BlobsFallidos + blobsFallidos,
            Participantes = acumulado.Participantes + participantes,
        };
    }

    private Task AuditarAsync(ReportePurgaCampanias reporte, CancellationToken cancellationToken)
    {
        var detalle =
            $"purga_total:camp={reporte.Campanias},conv={reporte.Conversaciones},msg={reporte.Mensajes}," +
            $"resp={reporte.Respuestas},eval={reporte.Evaluaciones},md={reporte.Artefactos}," +
            $"blobOk={reporte.BlobsBorrados},blobFail={reporte.BlobsFallidos},part={reporte.Participantes}," +
            $"usr={reporte.UsuariosBorrados}";

        return _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.AccionAdministrativa,
                usuarioId: null,
                numero: null,
                "purga_total",
                detalle,
                _correlacion.CorrelationIdActual,
                _tiempo.GetUtcNow()),
            cancellationToken);
    }
}
