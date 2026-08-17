using ElTejido.Application.Configuracion;
using ElTejido.Application.Conversacion;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using FluentAssertions;

namespace ElTejido.UnitTests.Conversacion;

/// <summary>
/// DT-P32-03 §3.1: tabla de política del único resolutor de cierre. El defecto que cierra esta
/// iniciativa es responder en español a un hilo en inglés, así que la ausencia de fallback cruzado se
/// prueba explícitamente.
/// </summary>
public sealed class ResolutorMensajeCierreCampaniaTests
{
    private const string CierreLegacy = "Gracias por participar.";
    private const string CierreIngles = "Thanks for taking part.";
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    public void GateApagado_ConservaElCierreLegacyEnCualquierIdiomaDelHilo(string idioma)
    {
        var resolutor = Crear(gateHabilitado: false);

        var resultado = resolutor.Resolver(CrearCampania(cierreIngles: CierreIngles), idioma);

        resultado.Should().BeOfType<ResultadoMensajeCierreCampania.Disponible>()
            .Which.Should().BeEquivalentTo(new
            {
                Texto = CierreLegacy,
                Idioma = "es",
                Origen = OrigenMensajeCierreCampania.Legacy,
            });
    }

    [Fact]
    public void GateEncendidoConHiloEspanol_UsaElRespaldoHistoricoDeLaCampania()
    {
        var resolutor = Crear(gateHabilitado: true);

        var resultado = resolutor.Resolver(CrearCampania(cierreIngles: CierreIngles), "es");

        var disponible = resultado.Should().BeOfType<ResultadoMensajeCierreCampania.Disponible>().Subject;
        disponible.Texto.Should().Be(CierreLegacy);
        disponible.Idioma.Should().Be("es");
        disponible.Origen.Should().Be(OrigenMensajeCierreCampania.Legacy);
    }

    [Fact]
    public void GateEncendidoConHiloIngles_UsaLaLocalizacionDelIdioma()
    {
        var resolutor = Crear(gateHabilitado: true);

        var resultado = resolutor.Resolver(CrearCampania(cierreIngles: CierreIngles), "EN");

        var disponible = resultado.Should().BeOfType<ResultadoMensajeCierreCampania.Disponible>().Subject;
        disponible.Texto.Should().Be(CierreIngles);
        disponible.Idioma.Should().Be("en");
        disponible.Origen.Should().Be(OrigenMensajeCierreCampania.LocalizacionCampania);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GateEncendidoSinCierreLocalizado_FallaTipificadoYNuncaCaeAEspanol(string? cierreIngles)
    {
        var resolutor = Crear(gateHabilitado: true);

        var resultado = resolutor.Resolver(CrearCampania(cierreIngles), "en");

        var noDisponible = resultado.Should().BeOfType<ResultadoMensajeCierreCampania.NoDisponible>().Subject;
        noDisponible.Codigo.Should().Be(ResolutorMensajeCierreCampania.CodigoLocalizacionIncompleta);
        noDisponible.Idioma.Should().Be("en");
    }

    [Fact]
    public void GateEncendidoConIdiomaNoHabilitadoEnLaCampania_FallaTipificado()
    {
        var resolutor = Crear(gateHabilitado: true);

        var resultado = resolutor.Resolver(CrearCampania(cierreIngles: null, bilingue: false), "en");

        resultado.Should().BeOfType<ResultadoMensajeCierreCampania.NoDisponible>()
            .Which.Codigo.Should().Be(ResolutorMensajeCierreCampania.CodigoLocalizacionIncompleta);
    }

    [Fact]
    public void GateEncendidoSinIdiomaEnElHilo_ResuelveComoEspanol()
    {
        var resolutor = Crear(gateHabilitado: true);

        var resultado = resolutor.Resolver(CrearCampania(cierreIngles: CierreIngles), idiomaConversacion: null);

        resultado.Should().BeOfType<ResultadoMensajeCierreCampania.Disponible>()
            .Which.Idioma.Should().Be("es");
    }

    private static ResolutorMensajeCierreCampania Crear(bool gateHabilitado)
        => new(new OpcionesCatalogoTextos { Habilitado = gateHabilitado });

    private static Campania CrearCampania(string? cierreIngles, bool bilingue = true)
    {
        var pregunta = Pregunta.Crear(
            "p_1", "Pregunta 1", "Instruccion", "categoria", 1, EstadoRegistro.Activo,
            rubricaRef: null, versionRubrica: null, promptRefs: null, 1,
            LimitesSeguridad.ParaPregunta(1500, 2),
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));

        var localizaciones = bilingue
            ? new Dictionary<string, LocalizacionCampania>(StringComparer.Ordinal)
            {
                ["en"] = LocalizacionCampania.Crear(
                    "en", "Campaign", "Description", "Objective", cierreIngles,
                    mensajesIniciales: null,
                    preguntas: new Dictionary<string, LocalizacionPregunta>(StringComparer.Ordinal)
                    {
                        ["p_1"] = new("Question 1", "Instruction"),
                    }),
            }
            : null;

        return Campania.Crear(
            "c_1", "Campania c_1", "Descripcion", "Objetivo", EstadoCampania.Activa,
            mensajesIniciales: null, new[] { pregunta },
            "rub_1",
            new Dictionary<string, string> { ["evaluar"] = "pr_eval" },
            "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, CierreLegacy),
            LimitesSeguridad.Crear(1500, 10, 2),
            usuariosHabilitados: null, Epoca, Epoca,
            idiomasHabilitados: bilingue ? new[] { "es", "en" } : null,
            localizaciones: localizaciones);
    }
}
