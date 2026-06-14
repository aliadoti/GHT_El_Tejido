using ElTejido.Application.Participantes;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;

namespace ElTejido.Application.WhatsApp;

/// <summary>
/// Procesa un <see cref="TrabajoEnvio"/> de la cola (05 §2.5): envia por el gateway (plantilla si
/// esta configurada, texto libre en su defecto), registra el <c>EnvioMensaje</c> append-only
/// (03 §3.5), actualiza el estado de envio del participante y reporta el resultado al job. Logica
/// pura de aplicacion; el <c>IHostedService</c> aporta la cola, el throttling y el logging.
/// </summary>
public sealed class ProcesadorEnvio
{
    private readonly IWhatsAppGateway _gateway;
    private readonly IRepositorioParticipantes _participantes;
    private readonly IAlmacenJobs _jobs;
    private readonly TimeProvider _tiempo;

    public ProcesadorEnvio(
        IWhatsAppGateway gateway,
        IRepositorioParticipantes participantes,
        IAlmacenJobs jobs,
        TimeProvider tiempo)
    {
        _gateway = gateway;
        _participantes = participantes;
        _jobs = jobs;
        _tiempo = tiempo;
    }

    public async Task<EnvioResultado> ProcesarAsync(TrabajoEnvio trabajo, CancellationToken cancellationToken)
    {
        var resultado = trabajo.Plantilla is not null
            ? await _gateway.EnviarPlantillaAsync(
                trabajo.Numero,
                trabajo.Plantilla,
                trabajo.Variables,
                trabajo.Tipo,
                cancellationToken)
            : await _gateway.EnviarTextoAsync(
                trabajo.Numero,
                trabajo.TextoLibre,
                trabajo.Tipo,
                cancellationToken);

        var ahora = _tiempo.GetUtcNow();
        var estado = resultado.Exito ? EstadoEnvio.Enviado : EstadoEnvio.Error;

        var envio = EnvioMensaje.Crear(
            "env_" + Guid.NewGuid().ToString("N"),
            trabajo.CampaniaId,
            trabajo.UsuarioId,
            trabajo.MensajeInicialId,
            NumeroWhatsApp.FromNormalized(trabajo.Numero),
            estado,
            trabajo.Tipo,
            resultado.WhatsappMessageId,
            ahora,
            resultado.Error);
        await _participantes.RegistrarEnvioAsync(envio, cancellationToken);

        await ActualizarParticipanteAsync(trabajo, estado, ahora, cancellationToken);

        _jobs.RegistrarResultado(trabajo.JobId, resultado.Exito);
        return resultado;
    }

    private async Task ActualizarParticipanteAsync(
        TrabajoEnvio trabajo,
        EstadoEnvio estado,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var participante = await _participantes.ObtenerParticipantePorUsuarioAsync(
            trabajo.CampaniaId,
            trabajo.UsuarioId,
            cancellationToken);
        if (participante is null)
        {
            return;
        }

        var fechaPrimerEnvio = estado == EstadoEnvio.Enviado
            ? participante.FechaPrimerEnvio ?? ahora
            : participante.FechaPrimerEnvio;

        var actualizado = ParticipanteCampania.Crear(
            participante.Id,
            participante.CampaniaId,
            participante.UsuarioId,
            participante.WhatsappNormalizado,
            participante.Estado,
            estado,
            participante.EstadoRespuesta,
            participante.FechaInclusion,
            fechaPrimerEnvio,
            participante.FechaUltimaRespuesta);

        await _participantes.GuardarParticipanteAsync(actualizado, cancellationToken);
    }
}
