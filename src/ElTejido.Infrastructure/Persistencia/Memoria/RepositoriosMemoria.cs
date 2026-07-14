using System.Collections.Concurrent;
using ElTejido.Application.Campanas;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Participantes;
using ElTejido.Application.Respuestas;
using ElTejido.Application.Seguridad;
using ElTejido.Application.Usuarios;
using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Conversaciones;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.Infrastructure.Persistencia.Memoria;

// Adaptadores in-memory para correr el MVP localmente (incluida la pagina /simulacion-whatsapp)
// sin Cosmos. Solo se registran cuando Persistencia:Modo = "Memoria" (ver
// ServiciosInfraestructura). Datos volatiles: se pierden al reiniciar la API.

internal sealed class RepositorioUsuariosMemoria : IRepositorioUsuarios
{
    private readonly ConcurrentDictionary<string, Usuario> _usuarios = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Tag> _tags = new(StringComparer.Ordinal);

    public Task GuardarUsuarioAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        _usuarios[usuario.Id] = usuario;
        return Task.CompletedTask;
    }

    public Task<Usuario?> ObtenerUsuarioPorIdAsync(string id, CancellationToken cancellationToken)
        => Task.FromResult(_usuarios.GetValueOrDefault(id));

    public Task<Usuario?> ObtenerUsuarioPorNumeroAsync(NumeroWhatsApp numero, CancellationToken cancellationToken)
        => Task.FromResult(_usuarios.Values.FirstOrDefault(u => u.WhatsappNormalizado.Valor == numero.Valor));

    public Task<IReadOnlyCollection<Usuario>> BuscarUsuariosAsync(FiltroUsuarios filtro, CancellationToken cancellationToken)
    {
        IEnumerable<Usuario> query = _usuarios.Values;
        if (filtro.Rol is not null)
        {
            query = query.Where(u => u.Rol == filtro.Rol);
        }

        if (filtro.Estado is not null)
        {
            query = query.Where(u => u.Estado == filtro.Estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Area))
        {
            query = query.Where(u => string.Equals(u.Area, filtro.Area, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Empresa))
        {
            query = query.Where(u => string.Equals(u.Empresa, filtro.Empresa, StringComparison.OrdinalIgnoreCase));
        }

        if (filtro.Tags.Count > 0)
        {
            query = query.Where(u => filtro.Tags.All(t => u.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            query = query.Where(u => u.Nombre.Contains(filtro.Busqueda, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyCollection<Usuario>>(query.ToArray());
    }

    public Task GuardarTagAsync(Tag tag, CancellationToken cancellationToken)
    {
        _tags[tag.Id] = tag;
        return Task.CompletedTask;
    }

    public Task<Tag?> ObtenerTagPorIdAsync(string id, CancellationToken cancellationToken)
        => Task.FromResult(_tags.GetValueOrDefault(id));

    public Task<IReadOnlyCollection<Tag>> BuscarTagsAsync(FiltroTags filtro, CancellationToken cancellationToken)
    {
        IEnumerable<Tag> query = _tags.Values;
        if (!string.IsNullOrWhiteSpace(filtro.TipoTag))
        {
            query = query.Where(t => string.Equals(t.TipoTag, filtro.TipoTag, StringComparison.OrdinalIgnoreCase));
        }

        if (filtro.Estado is not null)
        {
            query = query.Where(t => t.Estado == filtro.Estado);
        }

        return Task.FromResult<IReadOnlyCollection<Tag>>(query.ToArray());
    }
}

internal sealed class RepositorioCampaniasMemoria : IRepositorioCampanias
{
    private readonly ConcurrentDictionary<string, Campania> _campanias = new(StringComparer.Ordinal);

    public Task GuardarCampaniaAsync(Campania campania, CancellationToken cancellationToken)
    {
        _campanias[campania.Id] = campania;
        return Task.CompletedTask;
    }

    public Task<Campania?> ObtenerCampaniaPorIdAsync(string id, CancellationToken cancellationToken)
        => Task.FromResult(_campanias.GetValueOrDefault(id));

    public Task<IReadOnlyCollection<Campania>> BuscarCampaniasAsync(FiltroCampanias filtro, CancellationToken cancellationToken)
    {
        IEnumerable<Campania> query = _campanias.Values;
        if (filtro.Estado is not null)
        {
            query = query.Where(c => c.Estado == filtro.Estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            query = query.Where(c => c.Nombre.Contains(filtro.Busqueda, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyCollection<Campania>>(query.ToArray());
    }
}

internal sealed class RepositorioParticipantesMemoria : IRepositorioParticipantes
{
    private readonly ConcurrentDictionary<string, ParticipanteCampania> _participantes = new(StringComparer.Ordinal);
    private readonly List<EnvioMensaje> _envios = [];
    private readonly object _envioLock = new();

    public Task GuardarParticipanteAsync(ParticipanteCampania participante, CancellationToken cancellationToken)
    {
        _participantes[participante.Id] = participante;
        return Task.CompletedTask;
    }

    public Task<ParticipanteCampania?> ObtenerParticipantePorNumeroAsync(string campaniaId, NumeroWhatsApp numero, CancellationToken cancellationToken)
        => Task.FromResult(_participantes.Values.FirstOrDefault(p => p.CampaniaId == campaniaId && p.WhatsappNormalizado.Valor == numero.Valor));

    public Task<ParticipanteCampania?> ObtenerParticipantePorUsuarioAsync(string campaniaId, string usuarioId, CancellationToken cancellationToken)
        => Task.FromResult(_participantes.Values.FirstOrDefault(p => p.CampaniaId == campaniaId && p.UsuarioId == usuarioId));

    public Task<IReadOnlyCollection<ParticipanteCampania>> ListarParticipantesAsync(string campaniaId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<ParticipanteCampania>>(_participantes.Values.Where(p => p.CampaniaId == campaniaId).ToArray());

    public Task<IReadOnlyCollection<ParticipanteCampania>> BuscarParticipantesPorNumeroAsync(NumeroWhatsApp numero, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<ParticipanteCampania>>(_participantes.Values.Where(p => p.WhatsappNormalizado.Valor == numero.Valor).ToArray());

    public Task RegistrarEnvioAsync(EnvioMensaje envio, CancellationToken cancellationToken)
    {
        lock (_envioLock)
        {
            _envios.Add(envio);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExisteEnvioAsync(string campaniaId, string usuarioId, TipoEnvioMensaje tipo, string? mensajeInicialId, CancellationToken cancellationToken)
    {
        lock (_envioLock)
        {
            var existe = _envios.Any(e => e.CampaniaId == campaniaId
                && e.UsuarioId == usuarioId
                && e.Tipo == tipo
                && e.MensajeInicialId == mensajeInicialId);
            return Task.FromResult(existe);
        }
    }

    public Task<IReadOnlyCollection<EnvioMensaje>> ListarEnviosAsync(string campaniaId, CancellationToken cancellationToken)
    {
        lock (_envioLock)
        {
            IReadOnlyCollection<EnvioMensaje> envios = _envios.Where(e => e.CampaniaId == campaniaId).ToArray();
            return Task.FromResult(envios);
        }
    }
}

internal sealed class RepositorioConfiguracionMemoria : IRepositorioConfiguracion
{
    private readonly List<Rubrica> _rubricas = [];
    private readonly List<Prompt> _prompts = [];
    private readonly ConcurrentDictionary<string, ConfigLlm> _configs = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public Task GuardarRubricaAsync(Rubrica rubrica, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _rubricas.RemoveAll(r => r.Id == rubrica.Id && r.Version == rubrica.Version);
            _rubricas.Add(rubrica);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<Rubrica>> BuscarRubricasAsync(EstadoRubrica? estado, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            IEnumerable<Rubrica> query = _rubricas;
            if (estado is not null)
            {
                query = query.Where(r => r.Estado == estado);
            }

            var ultimas = query
                .GroupBy(r => r.Id)
                .Select(g => g.OrderByDescending(r => r.Version).First())
                .ToArray();
            return Task.FromResult<IReadOnlyCollection<Rubrica>>(ultimas);
        }
    }

    public Task<IReadOnlyCollection<Rubrica>> ListarVersionesRubricaAsync(string id, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var versiones = _rubricas.Where(r => r.Id == id).OrderByDescending(r => r.Version).ToArray();
            return Task.FromResult<IReadOnlyCollection<Rubrica>>(versiones);
        }
    }

    public Task<Rubrica?> ObtenerUltimaRubricaAsync(string id, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var ultima = _rubricas.Where(r => r.Id == id).OrderByDescending(r => r.Version).FirstOrDefault();
            return Task.FromResult(ultima);
        }
    }

    public Task GuardarPromptAsync(Prompt prompt, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _prompts.RemoveAll(p => p.Id == prompt.Id && p.Version == prompt.Version);
            _prompts.Add(prompt);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<Prompt>> BuscarPromptsAsync(string? tipoPrompt, EstadoPrompt? estado, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            IEnumerable<Prompt> query = _prompts;
            if (!string.IsNullOrWhiteSpace(tipoPrompt))
            {
                query = query.Where(p => p.TipoPrompt == tipoPrompt);
            }

            if (estado is not null)
            {
                query = query.Where(p => p.Estado == estado);
            }

            var ultimos = query
                .GroupBy(p => p.Id)
                .Select(g => g.OrderByDescending(p => p.Version).First())
                .ToArray();
            return Task.FromResult<IReadOnlyCollection<Prompt>>(ultimos);
        }
    }

    public Task<IReadOnlyCollection<Prompt>> ListarVersionesPromptAsync(string id, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var versiones = _prompts.Where(p => p.Id == id).OrderByDescending(p => p.Version).ToArray();
            return Task.FromResult<IReadOnlyCollection<Prompt>>(versiones);
        }
    }

    public Task<Prompt?> ObtenerUltimoPromptAsync(string id, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var ultimo = _prompts.Where(p => p.Id == id).OrderByDescending(p => p.Version).FirstOrDefault();
            return Task.FromResult(ultimo);
        }
    }

    public Task GuardarConfigLlmAsync(ConfigLlm config, CancellationToken cancellationToken)
    {
        _configs[config.Id] = config;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<ConfigLlm>> BuscarConfigsLlmAsync(EstadoRegistro? estado, CancellationToken cancellationToken)
    {
        IEnumerable<ConfigLlm> query = _configs.Values;
        if (estado is not null)
        {
            query = query.Where(c => c.Estado == estado);
        }

        return Task.FromResult<IReadOnlyCollection<ConfigLlm>>(query.ToArray());
    }

    public Task<ConfigLlm?> ObtenerConfigLlmAsync(string id, CancellationToken cancellationToken)
        => Task.FromResult(_configs.GetValueOrDefault(id));
}

internal sealed class RepositorioRespuestasMemoria : IRepositorioRespuestas
{
    private readonly ConcurrentDictionary<string, Respuesta> _respuestas = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DominioEvaluacion> _evaluaciones = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ArtefactoMarkdown> _artefactos = new(StringComparer.Ordinal);

    public Task GuardarRespuestaAsync(Respuesta respuesta, CancellationToken cancellationToken)
    {
        _respuestas[respuesta.Id] = respuesta;
        return Task.CompletedTask;
    }

    public Task<Respuesta?> ObtenerRespuestaAsync(string campaniaId, string respuestaId, CancellationToken cancellationToken)
        => Task.FromResult(_respuestas.Values.FirstOrDefault(r => r.CampaniaId == campaniaId && r.Id == respuestaId));

    public Task GuardarEvaluacionAsync(DominioEvaluacion evaluacion, CancellationToken cancellationToken)
    {
        _evaluaciones[evaluacion.RespuestaId] = evaluacion;
        return Task.CompletedTask;
    }

    public Task<DominioEvaluacion?> ObtenerEvaluacionPorRespuestaAsync(string campaniaId, string respuestaId, CancellationToken cancellationToken)
        => Task.FromResult(_evaluaciones.GetValueOrDefault(respuestaId));

    public Task<DominioEvaluacion?> ObtenerEvaluacionPorIdAsync(string campaniaId, string evaluacionId, CancellationToken cancellationToken)
        => Task.FromResult(_evaluaciones.Values.FirstOrDefault(e => e.Id == evaluacionId));

    public Task<IReadOnlyCollection<Respuesta>> ListarRespuestasAsync(string campaniaId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<Respuesta>>(_respuestas.Values.Where(r => r.CampaniaId == campaniaId).ToArray());

    public Task<int> ContarEvaluacionesUsuarioAsync(string campaniaId, string usuarioId, CancellationToken cancellationToken)
        => Task.FromResult(_evaluaciones.Values.Count(e => e.CampaniaId == campaniaId && e.UsuarioId == usuarioId));

    public Task<long> SumarTokensCampaniaAsync(string campaniaId, CancellationToken cancellationToken)
        => Task.FromResult(_evaluaciones.Values
            .Where(e => e.CampaniaId == campaniaId)
            .Sum(e => (long)(e.UsoTokens?.Total ?? 0)));

    public Task GuardarArtefactoAsync(ArtefactoMarkdown artefacto, CancellationToken cancellationToken)
    {
        _artefactos[artefacto.Id] = artefacto;
        return Task.CompletedTask;
    }

    public Task<ArtefactoMarkdown?> ObtenerArtefactoAsync(string campaniaId, string artefactoId, CancellationToken cancellationToken)
        => Task.FromResult(_artefactos.Values.FirstOrDefault(a => a.CampaniaId == campaniaId && a.Id == artefactoId));

    public Task<IReadOnlyCollection<ArtefactoMarkdown>> ListarArtefactosAsync(string campaniaId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<ArtefactoMarkdown>>(_artefactos.Values.Where(a => a.CampaniaId == campaniaId).ToArray());

    public Task<ConteoBorradoRespuestas> EliminarPorUsuarioAsync(string campaniaId, string? usuarioId, CancellationToken cancellationToken)
    {
        bool EnAlcance(string campania, string usuario)
            => campania == campaniaId && (usuarioId is null || usuario == usuarioId);

        var respuestas = _respuestas.Values.Where(r => EnAlcance(r.CampaniaId, r.UsuarioId)).ToArray();
        var evaluaciones = _evaluaciones.Values.Where(e => EnAlcance(e.CampaniaId, e.UsuarioId)).ToArray();
        var artefactos = _artefactos.Values.Where(a => EnAlcance(a.CampaniaId, a.UsuarioId)).ToArray();

        foreach (var respuesta in respuestas)
        {
            _respuestas.TryRemove(respuesta.Id, out _);
        }

        foreach (var evaluacion in evaluaciones)
        {
            _evaluaciones.TryRemove(evaluacion.RespuestaId, out _);
        }

        foreach (var artefacto in artefactos)
        {
            _artefactos.TryRemove(artefacto.Id, out _);
        }

        var rutas = artefactos
            .Select(a => a.BlobPath)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult(new ConteoBorradoRespuestas(respuestas.Length, evaluaciones.Length, artefactos.Length, rutas));
    }
}

internal sealed class RepositorioConversacionesMemoria : IRepositorioConversaciones
{
    private readonly ConcurrentDictionary<string, DominioConversacion> _conversaciones = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _ultimaActividad = new(StringComparer.Ordinal);
    private readonly List<Mensaje> _mensajes = [];
    private readonly object _msgLock = new();

    public Task GuardarConversacionAsync(DominioConversacion conversacion, CancellationToken cancellationToken)
    {
        _conversaciones[conversacion.Id] = conversacion;
        _ultimaActividad[conversacion.Id] = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<DominioConversacion>> ListarAbiertasInactivasAsync(DateTimeOffset limite, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<DominioConversacion>>(
            _conversaciones.Values
                .Where(c => c.Estado == EstadoConversacion.Abierta
                    && _ultimaActividad.TryGetValue(c.Id, out var actividad)
                    && actividad < limite)
                .ToArray());

    public Task<DominioConversacion?> ObtenerConversacionAsync(string campaniaId, string conversacionId, CancellationToken cancellationToken)
    {
        var conv = _conversaciones.GetValueOrDefault(conversacionId);
        if (conv is not null && conv.CampaniaId != campaniaId)
        {
            conv = null;
        }

        return Task.FromResult(conv);
    }

    public Task<IReadOnlyCollection<DominioConversacion>> ListarConversacionesAsync(string campaniaId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<DominioConversacion>>(_conversaciones.Values.Where(c => c.CampaniaId == campaniaId).ToArray());

    public Task<IReadOnlyCollection<Mensaje>> ListarMensajesAsync(string campaniaId, string conversacionId, CancellationToken cancellationToken)
    {
        lock (_msgLock)
        {
            IReadOnlyCollection<Mensaje> result = _mensajes
                .Where(m => m.CampaniaId == campaniaId && m.ConversacionId == conversacionId)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    public Task GuardarMensajeAsync(Mensaje mensaje, CancellationToken cancellationToken)
    {
        lock (_msgLock)
        {
            _mensajes.Add(mensaje);
        }

        return Task.CompletedTask;
    }

    public Task<ConteoBorradoConversaciones> EliminarPorUsuarioAsync(string campaniaId, string? usuarioId, CancellationToken cancellationToken)
    {
        var conversaciones = _conversaciones.Values
            .Where(c => c.CampaniaId == campaniaId && (usuarioId is null || c.UsuarioId == usuarioId))
            .ToArray();
        var idsConversaciones = conversaciones.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var conversacion in conversaciones)
        {
            _conversaciones.TryRemove(conversacion.Id, out _);
            _ultimaActividad.TryRemove(conversacion.Id, out _);
        }

        int mensajesBorrados;
        lock (_msgLock)
        {
            mensajesBorrados = _mensajes.RemoveAll(m => m.CampaniaId == campaniaId && idsConversaciones.Contains(m.ConversacionId));
        }

        return Task.FromResult(new ConteoBorradoConversaciones(conversaciones.Length, mensajesBorrados));
    }
}

internal sealed class RepositorioCodigosAuthMemoria : IRepositorioCodigosAuth
{
    private readonly ConcurrentDictionary<string, CodigoAuthAdmin> _codigos = new(StringComparer.Ordinal);

    public Task GuardarAsync(CodigoAuthAdmin codigo, CancellationToken cancellationToken)
    {
        _codigos[codigo.Id] = codigo;
        return Task.CompletedTask;
    }

    public Task<CodigoAuthAdmin?> ObtenerVigenteMasRecienteAsync(NumeroWhatsApp numero, CancellationToken cancellationToken)
    {
        var ultimo = _codigos.Values
            .Where(c => c.Numero.Valor == numero.Valor)
            .OrderByDescending(c => c.CreadoEn)
            .FirstOrDefault();
        return Task.FromResult(ultimo);
    }
}

internal sealed class RepositorioLogSeguridadMemoria : IRepositorioLogSeguridad
{
    public Task RegistrarAsync(LogSeguridad log, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class RegistroWebhookDedupeMemoria : IRegistroWebhookDedupe
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _vistos = new(StringComparer.Ordinal);

    public Task<bool> IntentarRegistrarMensajeAsync(string whatsappMessageId, DateTimeOffset procesadoEn, CancellationToken cancellationToken)
        => Task.FromResult(_vistos.TryAdd(whatsappMessageId, procesadoEn));
}
