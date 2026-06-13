using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Participantes;
using ElTejido.Application.Seguridad;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;

namespace ElTejido.Application.Identidad;

/// <summary>
/// Resuelve un numero entrante al participante autorizado para una campania activa (06 §3.2,
/// REQ §26.3). Todo rechazo es neutral de cara al usuario y se registra en <c>LogSeguridad</c>
/// (06 §3.3). La comparacion usa siempre el numero normalizado (REQ §10.3.2).
/// </summary>
public sealed class ResolutorParticipante : IResolutorParticipante
{
    private readonly INormalizadorNumero _normalizador;
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IRepositorioParticipantes _participantes;
    private readonly IRepositorioCampanias _campanias;
    private readonly IRepositorioLogSeguridad _logSeguridad;
    private readonly IProveedorCorrelacion _correlacion;
    private readonly TimeProvider _tiempo;

    public ResolutorParticipante(
        INormalizadorNumero normalizador,
        IRepositorioUsuarios usuarios,
        IRepositorioParticipantes participantes,
        IRepositorioCampanias campanias,
        IRepositorioLogSeguridad logSeguridad,
        IProveedorCorrelacion correlacion,
        TimeProvider tiempo)
    {
        _normalizador = normalizador;
        _usuarios = usuarios;
        _participantes = participantes;
        _campanias = campanias;
        _logSeguridad = logSeguridad;
        _correlacion = correlacion;
        _tiempo = tiempo;
    }

    public async Task<ResultadoResolucion> ResolverAsync(string numeroCrudo, CancellationToken cancellationToken)
    {
        if (!_normalizador.TryNormalizar(numeroCrudo, out var numeroOpt) || numeroOpt is null)
        {
            return await RechazarAsync(MotivoRechazo.NoMatriculado, null, null, cancellationToken);
        }

        var numero = numeroOpt;

        var usuario = await _usuarios.ObtenerUsuarioPorNumeroAsync(numero, cancellationToken);
        if (usuario is null)
        {
            return await RechazarAsync(MotivoRechazo.NoMatriculado, numero, null, cancellationToken);
        }

        if (usuario.Estado != EstadoRegistro.Activo)
        {
            return await RechazarAsync(MotivoRechazo.Inactivo, numero, usuario.Id, cancellationToken);
        }

        if (usuario.Rol != RolUsuario.Participante)
        {
            return await RechazarAsync(MotivoRechazo.NoEsParticipante, numero, usuario.Id, cancellationToken);
        }

        var elegido = await ResolverCampaniaActivaAsync(numero, cancellationToken);
        if (elegido is null)
        {
            return await RechazarAsync(MotivoRechazo.SinCampaniaActiva, numero, usuario.Id, cancellationToken);
        }

        var (participante, campania) = elegido.Value;

        var preguntaVigente = campania.Preguntas
            .Where(pregunta => pregunta.Estado == EstadoRegistro.Activo)
            .OrderBy(pregunta => pregunta.Orden)
            .FirstOrDefault();

        if (preguntaVigente is null)
        {
            return await RechazarAsync(MotivoRechazo.SinPreguntaVigente, numero, usuario.Id, cancellationToken);
        }

        return new ResultadoResolucion.Autorizado(
            new ParticipanteResuelto(usuario, campania, participante, preguntaVigente));
    }

    /// <summary>
    /// MVP: se asume una campania activa por participante. Si hubiera varias, se elige la
    /// asociacion mas reciente por ultima respuesta o, en su defecto, por fecha de inclusion
    /// (06 §3.2 nota). Se documenta el supuesto en SUPUESTOS.md.
    /// </summary>
    private async Task<(ParticipanteCampania Participante, Campania Campania)?> ResolverCampaniaActivaAsync(
        NumeroWhatsApp numero,
        CancellationToken cancellationToken)
    {
        var asociaciones = await _participantes.BuscarParticipantesPorNumeroAsync(numero, cancellationToken);

        var candidatos = new List<(ParticipanteCampania Participante, Campania Campania)>();
        foreach (var asociacion in asociaciones)
        {
            if (asociacion.Estado != EstadoRegistro.Activo)
            {
                continue;
            }

            var campania = await _campanias.ObtenerCampaniaPorIdAsync(asociacion.CampaniaId, cancellationToken);
            if (campania is null || campania.Estado != EstadoCampania.Activa)
            {
                continue;
            }

            candidatos.Add((asociacion, campania));
        }

        if (candidatos.Count == 0)
        {
            return null;
        }

        return candidatos
            .OrderByDescending(candidato =>
                candidato.Participante.FechaUltimaRespuesta ?? candidato.Participante.FechaInclusion)
            .First();
    }

    private async Task<ResultadoResolucion> RechazarAsync(
        MotivoRechazo motivo,
        NumeroWhatsApp? numero,
        string? usuarioId,
        CancellationToken cancellationToken)
    {
        var log = LogSeguridad.Crear(
            "log_" + Guid.NewGuid().ToString("N"),
            TipoEventoSeguridad.RechazoParticipacion,
            usuarioId,
            numero?.Valor,
            "rechazado",
            motivo.ToString(),
            _correlacion.CorrelationIdActual,
            _tiempo.GetUtcNow());

        await _logSeguridad.RegistrarAsync(log, cancellationToken);
        return new ResultadoResolucion.NoAutorizado(motivo);
    }
}
