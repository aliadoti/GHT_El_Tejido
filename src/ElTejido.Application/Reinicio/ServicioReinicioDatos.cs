using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Markdown;
using ElTejido.Application.Participantes;
using ElTejido.Application.Respuestas;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Seguridad;

namespace ElTejido.Application.Reinicio;

/// <summary>
/// Implementacion de P-03. Orquesta el borrado fisico por alcance en el orden seguro
/// <c>responses → conversations → reset de participantes</c> (03 §3.4-§3.10) y arma el reporte de
/// conteos. Es idempotente: reinvocarlo sobre datos ya limpios devuelve ceros sin error. Toda salida
/// queda auditada en <see cref="IRepositorioLogSeguridad"/> con conteos y correlationId; no registra PII.
/// </summary>
public sealed class ServicioReinicioDatos : IServicioReinicioDatos
{
    private readonly IRepositorioCampanias _campanias;
    private readonly IRepositorioParticipantes _participantes;
    private readonly IRepositorioConversaciones _conversaciones;
    private readonly IRepositorioRespuestas _respuestas;
    private readonly IAlmacenBlob _blob;
    private readonly IRepositorioLogSeguridad _logSeguridad;
    private readonly IProveedorCorrelacion _correlacion;
    private readonly TimeProvider _tiempo;

    public ServicioReinicioDatos(
        IRepositorioCampanias campanias,
        IRepositorioParticipantes participantes,
        IRepositorioConversaciones conversaciones,
        IRepositorioRespuestas respuestas,
        IAlmacenBlob blob,
        IRepositorioLogSeguridad logSeguridad,
        IProveedorCorrelacion correlacion,
        TimeProvider tiempo)
    {
        _campanias = campanias;
        _participantes = participantes;
        _conversaciones = conversaciones;
        _respuestas = respuestas;
        _blob = blob;
        _logSeguridad = logSeguridad;
        _correlacion = correlacion;
        _tiempo = tiempo;
    }

    public async Task<ReporteReinicioDatos> ReiniciarParticipanteAsync(
        string campaniaId,
        string usuarioId,
        bool reiniciarEnvios,
        CancellationToken cancellationToken)
    {
        var campania = await RequerirCampaniaAsync(campaniaId, cancellationToken);
        var idUsuario = RequerirTexto(usuarioId, "usuarioId");

        var reporte = await BorrarAlcanceAsync(campania.Id, idUsuario, cancellationToken);

        var participante = await _participantes.ObtenerParticipantePorUsuarioAsync(campania.Id, idUsuario, cancellationToken);
        if (participante is not null)
        {
            await ResetearParticipanteAsync(participante, reiniciarEnvios, cancellationToken);
            reporte = reporte with { ParticipantesReseteados = 1 };
        }

        await AuditarAsync(campania.Id, idUsuario, reporte, cancellationToken);
        return reporte;
    }

    public async Task<ReporteReinicioDatos> ReiniciarCampaniaAsync(
        string campaniaId,
        IReadOnlyCollection<string>? usuarioIds,
        bool reiniciarEnvios,
        CancellationToken cancellationToken)
    {
        var campania = await RequerirCampaniaAsync(campaniaId, cancellationToken);
        var subconjunto = usuarioIds?
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        ReporteReinicioDatos reporte;
        if (subconjunto is { Length: > 0 })
        {
            // Subconjunto explicito: se borra y resetea usuario por usuario.
            reporte = ReporteReinicioDatos.Vacio;
            foreach (var idUsuario in subconjunto)
            {
                var parcial = await BorrarAlcanceAsync(campania.Id, idUsuario, cancellationToken);
                var participante = await _participantes.ObtenerParticipantePorUsuarioAsync(campania.Id, idUsuario, cancellationToken);
                if (participante is not null)
                {
                    await ResetearParticipanteAsync(participante, reiniciarEnvios, cancellationToken);
                    parcial = parcial with { ParticipantesReseteados = 1 };
                }

                reporte = reporte.Sumar(parcial);
            }
        }
        else
        {
            // Toda la campania: un unico borrado en bloque (usuarioId = null) + reset de todos.
            reporte = await BorrarAlcanceAsync(campania.Id, usuarioId: null, cancellationToken);
            var participantes = await _participantes.ListarParticipantesAsync(campania.Id, cancellationToken);
            var reseteados = 0;
            foreach (var participante in participantes)
            {
                await ResetearParticipanteAsync(participante, reiniciarEnvios, cancellationToken);
                reseteados++;
            }

            reporte = reporte with { ParticipantesReseteados = reseteados };
        }

        await AuditarAsync(campania.Id, usuarioId: null, reporte, cancellationToken);
        return reporte;
    }

    // Borra respuestas/evaluaciones/artefactos, luego conversaciones/mensajes, luego los blobs de los
    // artefactos borrados (fallo tolerado). El orden responses → conversations evita dejar respuestas
    // huerfanas si la operacion se interrumpe (re-ejecutar completa la limpieza).
    private async Task<ReporteReinicioDatos> BorrarAlcanceAsync(
        string campaniaId,
        string? usuarioId,
        CancellationToken cancellationToken)
    {
        var respuestas = await _respuestas.EliminarPorUsuarioAsync(campaniaId, usuarioId, cancellationToken);
        var conversaciones = await _conversaciones.EliminarPorUsuarioAsync(campaniaId, usuarioId, cancellationToken);

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

        return new ReporteReinicioDatos(
            conversaciones.Conversaciones,
            conversaciones.Mensajes,
            respuestas.Respuestas,
            respuestas.Evaluaciones,
            respuestas.Artefactos,
            blobsBorrados,
            blobsFallidos,
            ParticipantesReseteados: 0);
    }

    private Task ResetearParticipanteAsync(
        ParticipanteCampania participante,
        bool reiniciarEnvios,
        CancellationToken cancellationToken)
    {
        // Conserva el participante; solo resetea su estado de respuesta (y, opcionalmente, el envio
        // para poder re-disparar el mensaje inicial desde el portal). Campos del dominio existentes
        // (03 §3.4): no cambia el contrato de datos.
        var reseteado = ParticipanteCampania.Crear(
            participante.Id,
            participante.CampaniaId,
            participante.UsuarioId,
            participante.WhatsappNormalizado,
            participante.Estado,
            reiniciarEnvios ? EstadoEnvio.Pendiente : participante.EstadoEnvio,
            EstadoRespuestaParticipante.SinRespuesta,
            participante.FechaInclusion,
            reiniciarEnvios ? null : participante.FechaPrimerEnvio,
            fechaUltimaRespuesta: null);

        return _participantes.GuardarParticipanteAsync(reseteado, cancellationToken);
    }

    private Task AuditarAsync(
        string campaniaId,
        string? usuarioId,
        ReporteReinicioDatos reporte,
        CancellationToken cancellationToken)
    {
        var alcance = usuarioId is null ? campaniaId : $"{campaniaId}:{usuarioId}";
        var detalle =
            $"reinicio_datos:{alcance}:conv={reporte.Conversaciones},msg={reporte.Mensajes}," +
            $"resp={reporte.Respuestas},eval={reporte.Evaluaciones},md={reporte.Artefactos}," +
            $"blobOk={reporte.BlobsBorrados},blobFail={reporte.BlobsFallidos},part={reporte.ParticipantesReseteados}";

        return _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.AccionAdministrativa,
                usuarioId,
                numero: null,
                "reinicio_datos",
                detalle,
                _correlacion.CorrelationIdActual,
                _tiempo.GetUtcNow()),
            cancellationToken);
    }

    private async Task<Campania> RequerirCampaniaAsync(string campaniaId, CancellationToken cancellationToken)
    {
        var campania = await _campanias.ObtenerCampaniaPorIdAsync(RequerirTexto(campaniaId, "campaniaId"), cancellationToken);
        return campania ?? throw new ErrorNoEncontrado("La campania no existe.");
    }

    private static string RequerirTexto(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ErrorValidacion($"El campo {campo} es obligatorio.", new[] { new DetalleError(campo, "obligatorio") });
        }

        return valor.Trim();
    }
}
