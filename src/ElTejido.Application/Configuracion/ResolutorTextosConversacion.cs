using ElTejido.Application.Conversacion;
using ConversacionDominio = ElTejido.Domain.Conversaciones.Conversacion;

namespace ElTejido.Application.Configuracion;

/// <summary>
/// Proyeccion unica de los textos que puede usar una conversacion. La conversacion aporta el
/// idioma que quedo fijado al iniciar su ciclo; el proveedor decide si se usa el catalogo o la
/// configuracion heredada segun el gate operativo.
/// </summary>
public interface IResolutorTextosConversacion
{
    Task<TextosConversacionResueltos> ResolverAsync(
        ConversacionDominio conversacion,
        CancellationToken cancellationToken);
}

/// <summary>
/// Contenido efectivo, inmutable durante la ejecucion que lo consume. La version solo existe
/// cuando el catalogo esta habilitado; con el gate apagado se conserva la semilla espanola que
/// replica la configuracion heredada.
/// </summary>
public sealed record TextosConversacionResueltos(
    string Idioma,
    IReadOnlyDictionary<string, string> Mensajes,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> Frases,
    OrigenTextosConversacion Origen,
    int? VersionCatalogo,
    string? HuellaCatalogo);

public sealed class ResolutorTextosConversacion : IResolutorTextosConversacion
{
    private readonly IProveedorTextosConversacion _proveedor;
    private readonly OpcionesConversacion _opcionesConversacion;

    public ResolutorTextosConversacion(
        IProveedorTextosConversacion proveedor,
        OpcionesConversacion opcionesConversacion)
    {
        _proveedor = proveedor;
        _opcionesConversacion = opcionesConversacion;
    }

    public async Task<TextosConversacionResueltos> ResolverAsync(
        ConversacionDominio conversacion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversacion);

        var resultado = await _proveedor.ObtenerParaRuntimeAsync(conversacion.Idioma, cancellationToken);
        if (resultado.Version is null)
        {
            // El proveedor solo devuelve null con el gate apagado. El comportamiento anterior no
            // tenia textos ingleses, por lo que esta rama debe seguir representando exactamente
            // los textos configurados del binario y no introducir una traduccion anticipada.
            var legado = CatalogosTextosSemilla.CrearSolicitud("es", _opcionesConversacion);
            return new TextosConversacionResueltos(
                legado.Idioma,
                legado.Mensajes,
                legado.Frases,
                resultado.Origen,
                VersionCatalogo: null,
                HuellaCatalogo: null);
        }

        var catalogo = resultado.Version.Catalogo;
        return new TextosConversacionResueltos(
            catalogo.Idioma,
            catalogo.Mensajes,
            catalogo.Frases,
            resultado.Origen,
            catalogo.Version,
            catalogo.Huella);
    }
}
