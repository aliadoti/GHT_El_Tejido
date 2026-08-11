using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Seguridad;
using ElTejido.Infrastructure.Persistencia.Memoria;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.Configuracion;

public sealed class CatalogosTextosConversacionTests
{
    [Fact]
    public void Validar_ContenidoCompleto_GeneraHuellaDeterminista()
    {
        var contenido = ContenidoValido("texto-control-no-auditar");
        var mensajesInvertidos = contenido.Mensajes.Reverse()
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        var primera = ValidadorCatalogoTextosConversacion.ValidarYCalcularHuella(
            contenido.Mensajes,
            contenido.Frases);
        var segunda = ValidadorCatalogoTextosConversacion.ValidarYCalcularHuella(
            mensajesInvertidos,
            contenido.Frases);

        primera.Should().Be(segunda).And.HaveLength(64);
    }

    [Fact]
    public void Validar_ClaveFaltanteYPlaceholderDesconocido_RechazaElCatalogo()
    {
        var contenido = ContenidoValido();
        var mensajes = contenido.Mensajes
            .Where(x => x.Key != "saludoPrimerContacto")
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        mensajes["acuseContinuar"] = "Hola {{secreto}}";

        var acto = () => ValidadorCatalogoTextosConversacion.ValidarYCalcularHuella(
            mensajes,
            contenido.Frases);

        var error = acto.Should().Throw<ErrorValidacion>().Which;
        error.Detalles.Should().Contain(x => x.Campo == "mensajes.saludoPrimerContacto");
        error.Detalles.Should().Contain(x => x.Problema == "placeholder_no_permitido:secreto");
    }

    [Fact]
    public void Validar_FrasesEquivalentesTrasNormalizar_RechazaDuplicado()
    {
        var contenido = ContenidoValido();
        var frases = contenido.Frases.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        frases["continuar"] = new[] { "Sí, continuar", "  SI,   CONTINUAR " };

        var acto = () => ValidadorCatalogoTextosConversacion.ValidarYCalcularHuella(
            contenido.Mensajes,
            frases);

        acto.Should().Throw<ErrorValidacion>()
            .Which.Detalles.Should().Contain(x => x.Problema == "frase_duplicada");
    }

    [Fact]
    public async Task Activar_NuevaVersion_InactivaAnteriorYConservaUnSoloActivo()
    {
        var repositorio = new RepositorioCatalogosTextosMemoria();
        var logs = Substitute.For<IRepositorioLogSeguridad>();
        var servicio = new ServicioGestionCatalogosTextos(repositorio, logs, TimeProvider.System);
        var contenido = ContenidoValido("texto-control-no-auditar");
        var primera = await servicio.CrearAsync(
            new SolicitudGuardarCatalogoTextos("conversacion-global", "es", contenido.Mensajes, contenido.Frases),
            "admin-1",
            CancellationToken.None);
        var activa1 = await servicio.ActivarAsync(
            "conversacion-global", "es", 1, primera.Etag, "admin-1", CancellationToken.None);
        var segunda = await servicio.CrearVersionAsync(
            "conversacion-global", "es", null, "admin-2", CancellationToken.None);

        await servicio.ActivarAsync(
            "conversacion-global", "es", 2, segunda.Etag, "admin-2", CancellationToken.None);

        var versiones = await repositorio.ListarVersionesAsync(
            "conversacion-global", "es", CancellationToken.None);
        versiones.Should().ContainSingle(x => x.Catalogo.Estado == EstadoCatalogoTextos.Activo)
            .Which.Catalogo.Version.Should().Be(2);
        versiones.Single(x => x.Catalogo.Version == 1).Catalogo.Estado.Should().Be(EstadoCatalogoTextos.Inactivo);
        activa1.Catalogo.Estado.Should().Be(EstadoCatalogoTextos.Activo);
        await logs.Received().RegistrarAsync(
            Arg.Is<LogSeguridad>(x =>
                x.TipoEvento == TipoEventoSeguridad.CatalogoTextosConversacion
                && x.Detalle != null
                && !x.Detalle.Contains("texto-control-no-auditar", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualizarBorrador_EtagObsoleto_DevuelveConflicto()
    {
        var repositorio = new RepositorioCatalogosTextosMemoria();
        var servicio = new ServicioGestionCatalogosTextos(
            repositorio,
            Substitute.For<IRepositorioLogSeguridad>(),
            TimeProvider.System);
        var contenido = ContenidoValido();
        var creado = await servicio.CrearAsync(
            new SolicitudGuardarCatalogoTextos("conversacion-global", "en", contenido.Mensajes, contenido.Frases),
            "admin",
            CancellationToken.None);
        await servicio.ActualizarBorradorAsync(
            "conversacion-global", "en", 1, contenido, creado.Etag, "admin", CancellationToken.None);

        var acto = () => servicio.ActualizarBorradorAsync(
            "conversacion-global", "en", 1, contenido, creado.Etag, "admin", CancellationToken.None);

        await acto.Should().ThrowAsync<ErrorConflicto>();
    }

    [Fact]
    public async Task Importar_FamiliaExistente_CreaNuevaVersionBorradorSinActivarla()
    {
        var repositorio = new RepositorioCatalogosTextosMemoria();
        var servicio = new ServicioGestionCatalogosTextos(
            repositorio,
            Substitute.For<IRepositorioLogSeguridad>(),
            TimeProvider.System);
        var contenido = ContenidoValido();
        var solicitud = new SolicitudGuardarCatalogoTextos(
            "conversacion-global", "es", contenido.Mensajes, contenido.Frases);
        var primera = await servicio.ImportarAsync(solicitud, "admin", CancellationToken.None);

        var segunda = await servicio.ImportarAsync(solicitud, "admin", CancellationToken.None);

        primera.Catalogo.Version.Should().Be(1);
        segunda.Catalogo.Version.Should().Be(2);
        segunda.Catalogo.Estado.Should().Be(EstadoCatalogoTextos.Borrador);
        (await repositorio.ObtenerActivoAsync("es", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Activar_InvalidaLaCacheDelIdioma()
    {
        var repositorio = new RepositorioCatalogosTextosMemoria();
        var invalidacion = Substitute.For<IInvalidacionCacheCatalogosTextos>();
        var servicio = new ServicioGestionCatalogosTextos(
            repositorio,
            Substitute.For<IRepositorioLogSeguridad>(),
            TimeProvider.System,
            invalidacion);
        var contenido = ContenidoValido();
        var creado = await servicio.CrearAsync(
            new SolicitudGuardarCatalogoTextos("conversacion-global", "en", contenido.Mensajes, contenido.Frases),
            "admin",
            CancellationToken.None);

        await servicio.ActivarAsync(
            "conversacion-global", "en", 1, creado.Etag, "admin", CancellationToken.None);

        invalidacion.Received(1).Invalidar("en");
    }

    private static SolicitudContenidoCatalogoTextos ContenidoValido(string sufijo = "base")
    {
        var mensajes = ValidadorCatalogoTextosConversacion.ClavesMensajes
            .ToDictionary(x => x, x => $"{x} {sufijo} {{{{nombre}}}}", StringComparer.Ordinal);
        var frases = ValidadorCatalogoTextosConversacion.ClavesFrases
            .ToDictionary(
                x => x,
                x => (IReadOnlyCollection<string>)new[] { $"{x} {sufijo}" },
                StringComparer.Ordinal);
        return new SolicitudContenidoCatalogoTextos(mensajes, frases);
    }
}
