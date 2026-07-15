using ElTejido.Calibracion;
using FluentAssertions;

namespace ElTejido.UnitTests.Calibracion;

/// <summary>
/// Paso 1 de D5: el loader tipado del golden set valida invariantes y el archivo semilla versionado
/// carga limpio. Sin LLM real: todo verde en CI.
/// </summary>
public sealed class CargadorGoldenSetTests
{
    [Fact]
    public void Cargar_ArchivoSemillasVersionado_CargaConCasosLimite()
    {
        var set = CargadorGoldenSet.CargarDesdeArchivo(RutasGoldenSet.ArchivoSemillas());

        set.Version.Should().NotBeNullOrWhiteSpace();
        set.Entradas.Should().HaveCountGreaterThanOrEqualTo(5);
        set.Entradas.Select(e => e.Id).Should().OnlyHaveUniqueItems();
        // Los casos límite exigidos por la spec (D5 §3.1) están presentes.
        set.Entradas.Should().Contain(e => e.Esperado!.EsHostil, "debe haber al menos una entrada hostil con instrucciones embebidas");
        set.Entradas.Should().Contain(e => e.Categoria == "multi-idea");
        set.Entradas.Should().Contain(e => e.Categoria == "texto-corto");
        set.Entradas.Should().Contain(e => e.Categoria == "no-quiero-seguir");
    }

    [Fact]
    public void Cargar_JsonValido_DeserializaCampos()
    {
        const string json = """
        {
          "version": "test-1",
          "descripcion": "prueba",
          "entradas": [
            {
              "id": "e1",
              "categoria": "cat",
              "textoRespuesta": "una idea concreta",
              "esperado": { "ejeDebil": "concrecion", "decision": "repreguntar", "ideasEsperadas": ["idea"], "esHostil": false },
              "notas": "nota"
            }
          ]
        }
        """;

        var set = CargadorGoldenSet.Cargar(json);

        var entrada = set.Entradas.Should().ContainSingle().Subject;
        entrada.Id.Should().Be("e1");
        entrada.TextoRespuesta.Should().Be("una idea concreta");
        entrada.Esperado!.Decision.Should().Be(DecisionCalibracion.Repreguntar);
        entrada.Esperado.IdeasEsperadas.Should().ContainSingle().Which.Should().Be("idea");
        entrada.Esperado.EsHostil.Should().BeFalse();
    }

    [Fact]
    public void Cargar_Vacio_Lanza()
    {
        var accion = () => CargadorGoldenSet.Cargar("   ");

        accion.Should().Throw<CalibracionException>();
    }

    [Fact]
    public void Cargar_SinEntradas_Lanza()
    {
        var accion = () => CargadorGoldenSet.Cargar("""{ "version": "v", "entradas": [] }""");

        accion.Should().Throw<CalibracionException>().WithMessage("*entradas*");
    }

    [Fact]
    public void Cargar_IdDuplicado_Lanza()
    {
        const string json = """
        {
          "version": "v",
          "entradas": [
            { "id": "dup", "categoria": "c", "textoRespuesta": "t", "esperado": { "esHostil": false } },
            { "id": "dup", "categoria": "c", "textoRespuesta": "t2", "esperado": { "esHostil": false } }
          ]
        }
        """;

        var accion = () => CargadorGoldenSet.Cargar(json);

        accion.Should().Throw<CalibracionException>().WithMessage("*duplicado*");
    }

    [Fact]
    public void Cargar_DecisionEsperadaInvalida_Lanza()
    {
        const string json = """
        {
          "version": "v",
          "entradas": [
            { "id": "e1", "categoria": "c", "textoRespuesta": "t", "esperado": { "decision": "tal_vez", "esHostil": false } }
          ]
        }
        """;

        var accion = () => CargadorGoldenSet.Cargar(json);

        accion.Should().Throw<CalibracionException>().WithMessage("*decisión esperada inválida*");
    }

    [Fact]
    public void Cargar_EntradaSinTexto_Lanza()
    {
        const string json = """
        {
          "version": "v",
          "entradas": [
            { "id": "e1", "categoria": "c", "textoRespuesta": "  ", "esperado": { "esHostil": false } }
          ]
        }
        """;

        var accion = () => CargadorGoldenSet.Cargar(json);

        accion.Should().Throw<CalibracionException>().WithMessage("*textoRespuesta*");
    }
}
