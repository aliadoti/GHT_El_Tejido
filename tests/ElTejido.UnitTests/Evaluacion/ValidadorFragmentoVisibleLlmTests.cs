using ElTejido.Application.Evaluacion;
using FluentAssertions;

namespace ElTejido.UnitTests.Evaluacion;

/// <summary>
/// DT-I20-02 §5.1/§7.1: contrato visible en texto plano. La guarda detecta <b>estructura</b> editorial
/// y etiquetas internas, no caracteres sueltos: contenido legítimo del participante como
/// <c>caja #3</c> se conserva intacto.
/// </summary>
public sealed class ValidadorFragmentoVisibleLlmTests
{
    /// <summary>
    /// §1: forma exacta reportada en WhatsApp el 2026-08-13. Es la regresión que da origen a la deuda.
    /// </summary>
    private const string CasoReportado =
        "Ya quedó claro que quieres comparar el almacenamiento en racks.\n"
        + "### Lo que ya queda claro\n"
        + "El objetivo y el punto de arribo.\n"
        + "### Lo que todavía falta\n"
        + "El criterio de comparación.\n"
        + "### Siguiente ajuste recomendado\n"
        + "Definir la métrica.";

    [Fact]
    public void CasoReportado_ConEncabezadosMarkdown_SeRechaza()
    {
        var resultado = ValidadorFragmentoVisibleLlm.Validar(CasoReportado, Retro(admitePregunta: true));

        resultado.EsValido.Should().BeFalse();
        resultado.Motivo.Should().Be(ValidadorFragmentoVisibleLlm.MotivoMarkdownEstructural);
    }

    [Theory]
    // §7.1.1 y §5.1: encabezado, viñeta, lista numerada, cita, separador, tabla y bloque de código.
    [InlineData("### Lo que ya queda claro")]
    [InlineData("Resumen del avance.\n# Estado general")]
    [InlineData("Tu idea avanza.\n- Definir la métrica\n- Fijar el responsable")]
    [InlineData("Tu idea avanza.\n* Definir la métrica")]
    [InlineData("Tu idea avanza.\n1. Definir la métrica\n2. Fijar el responsable")]
    [InlineData("Tu idea avanza.\n> Cita del proceso interno")]
    [InlineData("Tu idea avanza.\n---\nY sigue.")]
    [InlineData("| Eje | Detalle |\n| --- | --- |")]
    [InlineData("Tu idea avanza.\n```json\n{\"a\":1}\n```")]
    public void EstructuraMarkdown_SeRechaza(string texto)
        => ValidadorFragmentoVisibleLlm.Validar(texto, Retro(admitePregunta: true))
            .Motivo.Should().Be(ValidadorFragmentoVisibleLlm.MotivoMarkdownEstructural);

    [Theory]
    // §7.1.2 y §7.1.3: `#` dentro de una frase, saltos de línea sin estructura y guion intercalado
    // son texto conversacional válido; el contenido del participante no se toca.
    [InlineData("La diferencia está en la caja #3.")]
    [InlineData("Tu idea avanza.\nFalta precisar el criterio de comparación.")]
    [InlineData("El costo baja 2 - 3 puntos con esa ruta.")]
    [InlineData("Queda claro el objetivo y el punto de arribo.")]
    public void TextoPlanoConversacional_SeAcepta(string texto)
    {
        var resultado = ValidadorFragmentoVisibleLlm.Validar(texto, Retro(admitePregunta: true));

        resultado.EsValido.Should().BeTrue();
        resultado.Motivo.Should().BeNull();
    }

    [Theory]
    // §7.1.5: etiquetas internas del contrato y órdenes de proceso, en español e inglés.
    [InlineData("Tu idea avanza. ready_to_save: true")]
    [InlineData("Your idea is ready. save now.")]
    [InlineData("Devuelvo retroalimentacion_usuario con el texto.")]
    [InlineData("Tu idea avanza. Listo para guardar.")]
    [InlineData("Estado: maduro\nSeguimos mañana.")]
    [InlineData("Pregunta clave: ¿qué métrica usarías?")]
    [InlineData("Key question: which metric would you use?")]
    [InlineData("Lo que ya queda claro\nEl objetivo del piloto.")]
    [InlineData("What is still missing\nThe comparison criteria.")]
    public void EtiquetaInterna_SeRechaza(string texto)
        => ValidadorFragmentoVisibleLlm.Validar(texto, Retro(admitePregunta: true))
            .Motivo.Should().Be(ValidadorFragmentoVisibleLlm.MotivoEtiquetaInterna);

    [Fact]
    public void FraseQueContieneUnTituloInternoEnMedio_SeConserva()
    {
        // La etiqueta solo cuenta como título de sección: la misma frase dentro de una oración es
        // lenguaje natural legítimo y DT-I20-01 §4.1 permite explícitamente "queda claro".
        const string texto = "Lo que ya queda claro es que definiste el alcance y el responsable.";

        ValidadorFragmentoVisibleLlm.Validar(texto, Retro(admitePregunta: true)).EsValido.Should().BeTrue();
    }

    [Fact]
    public void EstadoComoPalabraDeNegocio_SeConserva()
        => ValidadorFragmentoVisibleLlm.Validar(
                "El estado de la bodega mejora con esa ruta.",
                Retro(admitePregunta: true))
            .EsValido.Should().BeTrue();

    [Fact]
    public void RetroConPreguntaCuandoElTurnoYaLlevaRepregunta_SeRechaza()
        => ValidadorFragmentoVisibleLlm.Validar(
                "Tu idea avanza. ¿Qué métrica usarías?",
                Retro(admitePregunta: false))
            .Motivo.Should().Be(ValidadorFragmentoVisibleLlm.MotivoCantidadPreguntas);

    [Fact]
    public void RetroSinRepreguntaSeparada_AdmiteUnaSolaPregunta()
    {
        ValidadorFragmentoVisibleLlm.Validar("Tu idea avanza. ¿Qué métrica usarías?", Retro(admitePregunta: true))
            .EsValido.Should().BeTrue();

        ValidadorFragmentoVisibleLlm.Validar(
                "¿Qué métrica usarías? ¿Y quién la mide?",
                Retro(admitePregunta: true))
            .Motivo.Should().Be(ValidadorFragmentoVisibleLlm.MotivoCantidadPreguntas);
    }

    [Theory]
    // §4.2: la repregunta es obligatoria y contiene exactamente una pregunta.
    [InlineData("", ValidadorFragmentoVisibleLlm.MotivoVacio)]
    [InlineData("   ", ValidadorFragmentoVisibleLlm.MotivoVacio)]
    [InlineData("Cuentame mas sobre la metrica.", ValidadorFragmentoVisibleLlm.MotivoCantidadPreguntas)]
    [InlineData("¿Qué métrica usarías? ¿Quién la mide?", ValidadorFragmentoVisibleLlm.MotivoCantidadPreguntas)]
    public void RepreguntaQueIncumpleElContrato_SeRechaza(string texto, string motivo)
        => ValidadorFragmentoVisibleLlm.Validar(
                texto,
                new ContextoFragmentoVisible(TipoFragmentoVisible.Repregunta, 600) { AdmitePregunta = true })
            .Motivo.Should().Be(motivo);

    [Fact]
    public void RepreguntaValida_SeAcepta()
        => ValidadorFragmentoVisibleLlm.Validar(
                "¿Con qué métrica compararías los dos puntos de arribo?",
                new ContextoFragmentoVisible(TipoFragmentoVisible.Repregunta, 600) { AdmitePregunta = true })
            .EsValido.Should().BeTrue();

    [Fact]
    public void RetroVacia_SeRechazaPorObligatoria()
        => ValidadorFragmentoVisibleLlm.Validar(null, Retro(admitePregunta: true))
            .Motivo.Should().Be(ValidadorFragmentoVisibleLlm.MotivoVacio);

    [Fact]
    public void FragmentosOpcionalesDeI20_AusentesSonValidos()
    {
        // I-20 §4.1: un puente nulo (o una pregunta nula en un acto sin pregunta) es salida legítima.
        ValidadorFragmentoVisibleLlm.Validar(null, new ContextoFragmentoVisible(TipoFragmentoVisible.Puente, 320))
            .EsValido.Should().BeTrue();
        ValidadorFragmentoVisibleLlm.Validar(null, new ContextoFragmentoVisible(TipoFragmentoVisible.Pregunta, 320))
            .EsValido.Should().BeTrue();
    }

    [Fact]
    public void ExcesoDeLongitud_SeReportaComoMotivoFijo()
        => ValidadorFragmentoVisibleLlm.Validar(new string('a', 61), Retro(admitePregunta: true, max: 60))
            .Motivo.Should().Be(ValidadorFragmentoVisibleLlm.MotivoLongitud);

    [Fact]
    public void SinMaximoAplicable_NoSeEvaluaLaLongitud()
        => ValidadorFragmentoVisibleLlm.Validar(new string('a', 5000), Retro(admitePregunta: true, max: 0))
            .EsValido.Should().BeTrue();

    [Fact]
    public void ElMotivoNuncaIncluyeElTextoRechazado()
    {
        // §8: la auditoría solo admite códigos de baja cardinalidad.
        var resultado = ValidadorFragmentoVisibleLlm.Validar(CasoReportado, Retro(admitePregunta: true));

        resultado.Motivo.Should().NotContain("racks").And.NotContain("###");
    }

    private static ContextoFragmentoVisible Retro(bool admitePregunta, int max = 600)
        => new(TipoFragmentoVisible.Retroalimentacion, max) { AdmitePregunta = admitePregunta };
}
