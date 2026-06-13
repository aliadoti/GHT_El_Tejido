using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using FluentAssertions;

namespace ElTejido.UnitTests.Campanas;

public sealed class CampaniaTests
{
    [Fact]
    public void Crear_PreservesCampaignContractFieldsAndNormalizesCollections()
    {
        var creadoEn = new DateTimeOffset(2026, 6, 12, 18, 0, 0, TimeSpan.FromHours(-5));
        var actualizadoEn = creadoEn.AddMinutes(30);
        var mensaje = CrearMensaje();
        var pregunta = CrearPregunta();

        var campania = Campania.Crear(
            " c_2026conv ",
            " Convencion 2026 - Ideas ",
            " Captura de ideas ",
            " Recolectar y evaluar ideas ",
            EstadoCampania.Activa,
            [mensaje],
            [pregunta],
            " r_general ",
            new Dictionary<string, string> { [" evaluar "] = " pr_eval ", [""] = "omitido" },
            " llm_default ",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, " Gracias. Tu aporte quedo registrado correctamente. "),
            LimitesSeguridad.Crear(1500, 10, 2),
            ["u_1", " ", "u_2", "u_1"],
            creadoEn,
            actualizadoEn);

        campania.Id.Should().Be("c_2026conv");
        campania.Nombre.Should().Be("Convencion 2026 - Ideas");
        campania.Descripcion.Should().Be("Captura de ideas");
        campania.Objetivo.Should().Be("Recolectar y evaluar ideas");
        campania.Estado.Should().Be(EstadoCampania.Activa);
        campania.PermiteInteraccion.Should().BeTrue();
        campania.MensajesIniciales.Should().ContainSingle().Which.Should().BeSameAs(mensaje);
        campania.Preguntas.Should().ContainSingle().Which.Should().BeSameAs(pregunta);
        campania.RubricaRef.Should().Be("r_general");
        campania.PromptRefs.Should().Contain("evaluar", "pr_eval");
        campania.PromptRefs.Should().NotContainKey("");
        campania.ConfigLlmRef.Should().Be("llm_default");
        campania.UsuariosHabilitados.Should().Equal("u_1", "u_2");
        campania.CreadoEn.Offset.Should().Be(TimeSpan.Zero);
        campania.ActualizadoEn.Offset.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(EstadoCampania.Borrador)]
    [InlineData(EstadoCampania.Cerrada)]
    [InlineData(EstadoCampania.Archivada)]
    public void PermiteInteraccion_IsFalseWhenCampaignIsNotActive(EstadoCampania estado)
    {
        var campania = CrearCampania(estado);

        campania.PermiteInteraccion.Should().BeFalse();
    }

    [Fact]
    public void Crear_RejectsUpdatedDateBeforeCreatedDate()
    {
        var creadoEn = DateTimeOffset.UtcNow;

        var act = () => Campania.Crear(
            "c_1",
            "Campania",
            "Descripcion",
            "Objetivo",
            EstadoCampania.Borrador,
            [],
            [],
            "r_general",
            null,
            "llm_default",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, "Gracias."),
            LimitesSeguridad.Crear(1500, 10, 2),
            [],
            creadoEn,
            creadoEn.AddTicks(-1));

        act.Should()
            .Throw<DomainValidationException>()
            .Where(exception => exception.Code == "FECHA_ACTUALIZACION_INVALIDA");
    }

    [Fact]
    public void Crear_RejectsRequiredFields()
    {
        var act = () => Campania.Crear(
            "",
            "Campania",
            "Descripcion",
            "Objetivo",
            EstadoCampania.Borrador,
            [],
            [],
            "r_general",
            null,
            "llm_default",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, "Gracias."),
            LimitesSeguridad.Crear(1500, 10, 2),
            [],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        act.Should()
            .Throw<DomainValidationException>()
            .Where(exception => exception.Code == "CAMPO_OBLIGATORIO");
    }

    private static Campania CrearCampania(EstadoCampania estado)
    {
        return Campania.Crear(
            "c_1",
            "Campania",
            "Descripcion",
            "Objetivo",
            estado,
            [],
            [],
            "r_general",
            null,
            "llm_default",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, "Gracias."),
            LimitesSeguridad.Crear(1500, 10, 2),
            [],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static MensajeInicial CrearMensaje()
    {
        return MensajeInicial.Crear(
            "mi_1",
            "saludo",
            "Hola {{nombre}}.",
            1,
            ["nombre", " campania ", "nombre", " "],
            EstadoRegistro.Activo,
            PlantillaWhatsApp.Crear("el_tejido_saludo", "es", ["nombre", "campania"]));
    }

    private static Pregunta CrearPregunta()
    {
        return Pregunta.Crear(
            "p_ingresos",
            "Escribe una idea para mejorar los ingresos.",
            "Se concreto.",
            "ingresos",
            1,
            EstadoRegistro.Activo,
            "r_general",
            3,
            new Dictionary<string, string> { ["evaluar"] = "pr_eval" },
            1,
            LimitesSeguridad.ParaPregunta(1500, 2),
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));
    }
}
