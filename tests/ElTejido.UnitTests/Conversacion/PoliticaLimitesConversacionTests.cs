using ElTejido.Application.Conversacion;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Respuestas;
using FluentAssertions;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;

namespace ElTejido.UnitTests.Conversacion;

/// <summary>
/// P-15 (CAL-001) — pruebas de la política determinista extraída del orquestador. Cubren la precedencia
/// del umbral (pregunta → campaña → global) incluida la campaña sin configuración opcional, la
/// clasificación de madurez, el corte por calificación alta con su kill-switch y la elegibilidad de mejora.
/// Reproducen las decisiones que antes vivían inline en <see cref="OrquestadorConversacion"/>.
/// </summary>
public sealed class PoliticaLimitesConversacionTests
{
    [Fact]
    public void ResolverUmbralResumen_RespetaPrecedenciaYNoAlteraUmbralBase()
    {
        var politica = new PoliticaLimitesConversacion(0.6, false, 0.4, true);
        var campania = CrearCampania(umbralCampania: 0.5, umbralResumenCampania: 0.45);
        var pregunta = CrearPregunta(umbralPregunta: 0.8, umbralResumenPregunta: 0.5);

        politica.ResolverUmbralBase(campania, pregunta).Should().Be(0.8);
        politica.ResolverUmbralResumen(campania, pregunta).Should().Be(0.5);
        politica.OrigenUmbralResumen(campania, pregunta).Should().Be("pregunta");
    }

    [Fact]
    public void ResolverUmbralResumen_KillSwitchApagado_DevuelveCero()
    {
        var politica = new PoliticaLimitesConversacion(0.6, false, 0.4, false);
        politica.ResolverUmbralResumen(CrearCampania(umbralCampania: null), CrearPregunta(umbralPregunta: null))
            .Should().Be(0);
    }
    private static readonly EscalaRubrica Escala1a5 = new(1, 5);
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    // ---- ResolverUmbralBase: precedencia pregunta → campaña → global -------------------------------

    [Fact]
    public void ResolverUmbralBase_CampaniaSinConfiguracionOpcional_UsaElDefaultGlobal()
    {
        var politica = new PoliticaLimitesConversacion(umbralBaseGlobal: 0.6, cierreAnticipadoHabilitado: false);

        politica.ResolverUmbralBase(CrearCampania(umbralCampania: null), CrearPregunta(umbralPregunta: null))
            .Should().Be(0.6);
    }

    [Fact]
    public void ResolverUmbralBase_OverrideDeCampania_GanaSobreElGlobal()
    {
        var politica = new PoliticaLimitesConversacion(umbralBaseGlobal: 0.6, cierreAnticipadoHabilitado: false);

        politica.ResolverUmbralBase(CrearCampania(umbralCampania: 0.4), CrearPregunta(umbralPregunta: null))
            .Should().Be(0.4);
    }

    [Fact]
    public void ResolverUmbralBase_OverrideDePregunta_GanaSobreCampaniaYGlobal()
    {
        var politica = new PoliticaLimitesConversacion(umbralBaseGlobal: 0.6, cierreAnticipadoHabilitado: false);

        politica.ResolverUmbralBase(CrearCampania(umbralCampania: 0.4), CrearPregunta(umbralPregunta: 0.9))
            .Should().Be(0.9);
    }

    [Theory]
    [InlineData(null, null, "global")]
    [InlineData(0.4, null, "campania")]
    [InlineData(0.4, 0.9, "pregunta")]
    [InlineData(null, 0.9, "pregunta")]
    public void OrigenUmbral_ReflejaLaFuenteEfectiva(double? umbralCampania, double? umbralPregunta, string esperado)
    {
        var politica = new PoliticaLimitesConversacion(umbralBaseGlobal: 0.6, cierreAnticipadoHabilitado: false);

        politica.OrigenUmbral(CrearCampania(umbralCampania), CrearPregunta(umbralPregunta)).Should().Be(esperado);
    }

    // ---- ResolverUmbralCierreAnticipado: kill-switch global ----------------------------------------

    [Fact]
    public void ResolverUmbralCierreAnticipado_KillSwitchApagado_DevuelveCeroAunqueHayaUmbral()
    {
        var politica = new PoliticaLimitesConversacion(umbralBaseGlobal: 0.6, cierreAnticipadoHabilitado: false);

        politica.ResolverUmbralCierreAnticipado(CrearCampania(umbralCampania: 0.4), CrearPregunta(umbralPregunta: 0.9))
            .Should().Be(0);
    }

    [Fact]
    public void ResolverUmbralCierreAnticipado_KillSwitchEncendido_UsaElUmbralBase()
    {
        var politica = new PoliticaLimitesConversacion(umbralBaseGlobal: 0.6, cierreAnticipadoHabilitado: true);

        politica.ResolverUmbralCierreAnticipado(CrearCampania(umbralCampania: 0.4), CrearPregunta(umbralPregunta: null))
            .Should().Be(0.4);
    }

    // ---- ValorUmbral / UmbralAlcanzado -------------------------------------------------------------

    [Fact]
    public void ValorUmbral_TraduceLaFraccionAlRangoDeLaEscala()
    {
        var politica = new PoliticaLimitesConversacion(umbralBaseGlobal: 0, cierreAnticipadoHabilitado: false);

        // Escala 1..5, fracción 0.6 -> 1 + 0.6 * 4 = 3.4.
        politica.ValorUmbral(Escala1a5, 0.6).Should().Be(3.4m);
    }

    [Fact]
    public void ValorUmbral_FraccionMayorQueUno_SeAcotaAlMaximoDeLaEscala()
    {
        var politica = new PoliticaLimitesConversacion(umbralBaseGlobal: 0, cierreAnticipadoHabilitado: false);

        politica.ValorUmbral(Escala1a5, 2.0).Should().Be(5m);
    }

    [Fact]
    public void UmbralAlcanzado_UmbralCeroONegativo_SiempreEsFalse()
    {
        var politica = new PoliticaLimitesConversacion(umbralBaseGlobal: 0, cierreAnticipadoHabilitado: false);

        politica.UmbralAlcanzado(5m, Escala1a5, 0).Should().BeFalse();
        politica.UmbralAlcanzado(5m, Escala1a5, -0.5).Should().BeFalse();
    }

    [Theory]
    [InlineData(3.4, true)] // igual al corte (3.4) cuenta como alcanzado
    [InlineData(4.0, true)]
    [InlineData(3.0, false)]
    public void UmbralAlcanzado_ComparaContraElCorteDeLaEscala(double calificacion, bool esperado)
    {
        var politica = new PoliticaLimitesConversacion(umbralBaseGlobal: 0, cierreAnticipadoHabilitado: false);

        politica.UmbralAlcanzado((decimal)calificacion, Escala1a5, 0.6).Should().Be(esperado);
    }

    // ---- ClasificarMadurez -------------------------------------------------------------------------

    [Fact]
    public void ClasificarMadurez_CalificacionSobreElCorte_EsMaduro()
    {
        var politica = new PoliticaLimitesConversacion(umbralBaseGlobal: 0.6, cierreAnticipadoHabilitado: false);

        politica.ClasificarMadurez(esFallback: false, calificacionTotal: 4m, Escala1a5, umbralBase: 0.6)
            .Should().Be(NivelMadurez.Maduro);
    }

    [Fact]
    public void ClasificarMadurez_CalificacionBajoElCorte_EsIncubacion()
    {
        var politica = new PoliticaLimitesConversacion(umbralBaseGlobal: 0.6, cierreAnticipadoHabilitado: false);

        politica.ClasificarMadurez(esFallback: false, calificacionTotal: 2m, Escala1a5, umbralBase: 0.6)
            .Should().Be(NivelMadurez.Incubacion);
    }

    [Fact]
    public void ClasificarMadurez_Fallback_EsIncubacionAunqueLaCalificacionSeaAlta()
    {
        var politica = new PoliticaLimitesConversacion(umbralBaseGlobal: 0.6, cierreAnticipadoHabilitado: false);

        politica.ClasificarMadurez(esFallback: true, calificacionTotal: 5m, Escala1a5, umbralBase: 0.6)
            .Should().Be(NivelMadurez.Incubacion);
    }

    // ---- PuedeOfrecerMejora (elegibilidad de repregunta) -------------------------------------------

    [Fact]
    public void PuedeOfrecerMejora_ConCupoDisponible_EsTrue()
    {
        var politica = new PoliticaLimitesConversacion(umbralBaseGlobal: 0.6, cierreAnticipadoHabilitado: false);
        var conversacion = CrearConversacion(repreguntasUsadas: 0);

        politica.PuedeOfrecerMejora(conversacion, CrearPregunta(umbralPregunta: null, maxRepreguntas: 1))
            .Should().BeTrue();
    }

    [Fact]
    public void PuedeOfrecerMejora_CupoAgotado_EsFalse()
    {
        var politica = new PoliticaLimitesConversacion(umbralBaseGlobal: 0.6, cierreAnticipadoHabilitado: false);
        var conversacion = CrearConversacion(repreguntasUsadas: 1);

        politica.PuedeOfrecerMejora(conversacion, CrearPregunta(umbralPregunta: null, maxRepreguntas: 1))
            .Should().BeFalse();
    }

    // ---- Fábricas locales --------------------------------------------------------------------------

    private static Pregunta CrearPregunta(double? umbralPregunta, int maxRepreguntas = 1, double? umbralResumenPregunta = null)
        => Pregunta.Crear(
            "p_1",
            "Pregunta 1",
            "Instruccion",
            "categoria",
            1,
            EstadoRegistro.Activo,
            rubricaRef: null,
            versionRubrica: null,
            promptRefs: null,
            maxRepreguntas,
            LimitesSeguridad.ParaPregunta(1500, 2),
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            umbralPregunta, umbralResumenPregunta);

    private static Campania CrearCampania(double? umbralCampania, double? umbralResumenCampania = null)
        => Campania.Crear(
            "c_1",
            "Campania c_1",
            "Descripcion",
            "Objetivo",
            EstadoCampania.Activa,
            mensajesIniciales: null,
            new[] { CrearPregunta(umbralPregunta: null) },
            rubricaRef: "rub_1",
            promptRefs: null,
            configLlmRef: "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Campania),
            ConfigConversacional.Crear(1, "Gracias por participar.", umbralCierreAnticipado: umbralCampania, umbralResumenConsolidacion: umbralResumenCampania),
            LimitesSeguridad.Crear(1500, 10, 2),
            usuariosHabilitados: null,
            Epoca,
            Epoca);

    private static DominioConversacion CrearConversacion(int repreguntasUsadas)
    {
        var conversacion = DominioConversacion.Iniciar("conv_c_1_u_1_p_1", "c_1", "u_1", "p_1", "whatsapp", null, Epoca);
        for (var i = 0; i < repreguntasUsadas; i++)
        {
            conversacion = conversacion.RegistrarRepregunta();
        }

        return conversacion;
    }
}
