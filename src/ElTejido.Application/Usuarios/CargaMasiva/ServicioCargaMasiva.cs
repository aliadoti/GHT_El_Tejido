using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;

namespace ElTejido.Application.Usuarios.CargaMasiva;

/// <summary>
/// Implementacion de la carga masiva (I-08). Parsea el archivo con el lector que soporte su extension,
/// procesa fila por fila (una fila mala no aborta el lote), hace upsert por numero normalizado (06 §2),
/// crea las tags faltantes y, si se pidio, asocia los usuarios a la campania reutilizando
/// <see cref="IServicioGestionCampanias.AsociarParticipantesAsync"/> (04 §5.3). La operacion queda
/// auditada en <see cref="IRepositorioLogSeguridad"/> con conteos y correlationId, sin PII.
/// </summary>
public sealed class ServicioCargaMasiva : IServicioCargaMasiva
{
    private const string TipoTagImportada = "importado";

    private readonly IReadOnlyList<ILectorArchivoParticipantes> _lectores;
    private readonly IRepositorioUsuarios _usuarios;
    private readonly INormalizadorNumero _normalizador;
    private readonly IServicioGestionCampanias _campanias;
    private readonly IRepositorioLogSeguridad _logSeguridad;
    private readonly IProveedorCorrelacion _correlacion;
    private readonly TimeProvider _tiempo;

    public ServicioCargaMasiva(
        IEnumerable<ILectorArchivoParticipantes> lectores,
        IRepositorioUsuarios usuarios,
        INormalizadorNumero normalizador,
        IServicioGestionCampanias campanias,
        IRepositorioLogSeguridad logSeguridad,
        IProveedorCorrelacion correlacion,
        TimeProvider tiempo)
    {
        _lectores = lectores.ToArray();
        _usuarios = usuarios;
        _normalizador = normalizador;
        _campanias = campanias;
        _logSeguridad = logSeguridad;
        _correlacion = correlacion;
        _tiempo = tiempo;
    }

    public async Task<ReporteCargaMasiva> CargarAsync(
        string nombreArchivo,
        Stream contenido,
        string? campaniaId,
        CancellationToken cancellationToken)
    {
        var lector = ResolverLector(nombreArchivo);
        var filas = await lector.LeerAsync(contenido, cancellationToken);

        var resultados = new List<ResultadoFilaCarga>(filas.Count);
        var numerosVistos = new HashSet<string>(StringComparer.Ordinal);
        var idsParaAsociar = new List<string>();
        var creados = 0;
        var actualizados = 0;
        var rechazados = 0;

        foreach (var fila in filas)
        {
            var resultado = await ProcesarFilaAsync(fila, numerosVistos, cancellationToken);
            resultados.Add(resultado);

            switch (resultado.Resultado)
            {
                case ResultadoCarga.Creado:
                    creados++;
                    idsParaAsociar.Add(resultado.UsuarioId!);
                    break;
                case ResultadoCarga.Actualizado:
                    actualizados++;
                    idsParaAsociar.Add(resultado.UsuarioId!);
                    break;
                default:
                    rechazados++;
                    break;
            }
        }

        var asociados = await AsociarSiCorrespondeAsync(campaniaId, idsParaAsociar, cancellationToken);

        var reporte = new ReporteCargaMasiva(filas.Count, creados, actualizados, rechazados, asociados, resultados);
        await AuditarAsync(campaniaId, reporte, cancellationToken);
        return reporte;
    }

    private async Task<ResultadoFilaCarga> ProcesarFilaAsync(
        FilaParticipanteCarga fila,
        HashSet<string> numerosVistos,
        CancellationToken cancellationToken)
    {
        // Campos obligatorios (los mismos que el alta individual: Usuario.Crear exige area/empresa).
        if (string.IsNullOrWhiteSpace(fila.Nombre)
            || string.IsNullOrWhiteSpace(fila.Numero)
            || string.IsNullOrWhiteSpace(fila.Area)
            || string.IsNullOrWhiteSpace(fila.Empresa))
        {
            return Rechazo(fila.Fila, MotivoRechazoCarga.FilaIncompleta);
        }

        if (!_normalizador.TryNormalizar(fila.Numero, out var numero) || numero is null)
        {
            return Rechazo(fila.Fila, MotivoRechazoCarga.NumeroInvalido);
        }

        if (!numerosVistos.Add(numero.Valor))
        {
            // El primero gana; los repetidos dentro del mismo archivo se rechazan.
            return Rechazo(fila.Fila, MotivoRechazoCarga.DuplicadoEnArchivo);
        }

        await AsegurarTagsAsync(fila.Tags, cancellationToken);

        var ahora = _tiempo.GetUtcNow();
        var existente = await _usuarios.ObtenerUsuarioPorNumeroAsync(numero, cancellationToken);
        if (existente is null)
        {
            var nuevo = Usuario.Crear(
                "u_" + Guid.NewGuid().ToString("N"),
                fila.Nombre!,
                numero,
                RolUsuario.Participante,
                EstadoRegistro.Activo,
                fila.Area!,
                fila.Empresa!,
                fila.Tags,
                propiedadesDinamicas: null,
                ahora,
                ahora);
            await _usuarios.GuardarUsuarioAsync(nuevo, cancellationToken);
            return new ResultadoFilaCarga(fila.Fila, ResultadoCarga.Creado, nuevo.Id, Motivo: null);
        }

        // Upsert: el roster del archivo manda para nombre/area/empresa; conserva rol/estado/creado para
        // no degradar un admin ni reactivar un inactivo. Las tags se reemplazan solo si la fila trae
        // alguna (evita borrar tags al recargar un archivo sin columna Tags).
        var tags = fila.Tags.Count > 0 ? fila.Tags : existente.Tags;
        var actualizado = Usuario.Crear(
            existente.Id,
            fila.Nombre!,
            numero,
            existente.Rol,
            existente.Estado,
            fila.Area!,
            fila.Empresa!,
            tags,
            existente.PropiedadesDinamicas,
            existente.CreadoEn,
            ahora);
        await _usuarios.GuardarUsuarioAsync(actualizado, cancellationToken);
        return new ResultadoFilaCarga(fila.Fila, ResultadoCarga.Actualizado, actualizado.Id, Motivo: null);
    }

    private async Task AsegurarTagsAsync(IReadOnlyCollection<string> tags, CancellationToken cancellationToken)
    {
        // Las tags del archivo son identificadores de catalogo (03 §users). Si alguna no existe, se crea
        // con tipo "importado" para que quede en el catalogo y sea filtrable como cualquier otra.
        foreach (var tagId in tags)
        {
            var existente = await _usuarios.ObtenerTagPorIdAsync(tagId, cancellationToken);
            if (existente is not null)
            {
                continue;
            }

            var tag = Tag.Crear(tagId, tagId, TipoTagImportada, descripcion: null, EstadoRegistro.Activo, _tiempo.GetUtcNow());
            await _usuarios.GuardarTagAsync(tag, cancellationToken);
        }
    }

    private async Task<int> AsociarSiCorrespondeAsync(
        string? campaniaId,
        IReadOnlyCollection<string> usuarioIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(campaniaId) || usuarioIds.Count == 0)
        {
            return 0;
        }

        var asociados = await _campanias.AsociarParticipantesAsync(
            campaniaId,
            new SolicitudAsociarParticipantes(usuarioIds.ToArray(), Filtro: null),
            cancellationToken);
        return asociados.Count;
    }

    private Task AuditarAsync(string? campaniaId, ReporteCargaMasiva reporte, CancellationToken cancellationToken)
    {
        var detalle =
            $"carga_masiva:campania={campaniaId ?? "-"}:total={reporte.TotalFilas}," +
            $"creado={reporte.Creados},actualizado={reporte.Actualizados}," +
            $"rechazado={reporte.Rechazados},asociado={reporte.Asociados}";

        return _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.AccionAdministrativa,
                usuarioId: null,
                numero: null,
                "carga_masiva",
                detalle,
                _correlacion.CorrelationIdActual,
                _tiempo.GetUtcNow()),
            cancellationToken);
    }

    private ILectorArchivoParticipantes ResolverLector(string nombreArchivo)
    {
        var extension = Path.GetExtension(nombreArchivo);
        var lector = _lectores.FirstOrDefault(l => l.Soporta(extension));
        return lector ?? throw new ErrorValidacion(
            "El formato del archivo no es soportado (solo .csv).",
            new[] { new DetalleError("archivo", "formato_no_soportado") });
    }

    private static ResultadoFilaCarga Rechazo(int fila, string motivo)
        => new(fila, ResultadoCarga.Rechazado, UsuarioId: null, motivo);
}
