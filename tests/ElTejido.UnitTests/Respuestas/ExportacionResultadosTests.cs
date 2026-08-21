using ElTejido.Application.Common;
using ElTejido.Application.Respuestas;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Evaluacion;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Usuarios;
using FluentAssertions;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.UnitTests.Respuestas;

/// <summary>
/// P-34 §4.5: construcción de las exportaciones. Es lógica pura —arma filas de texto—, así que se
/// prueba sin archivos ni HTTP.
/// </summary>
public sealed class ExportacionResultadosTests
{
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    [Theory]
    [InlineData("", RecursoExportacion.Ideas)]
    [InlineData("ideas", RecursoExportacion.Ideas)]
    [InlineData("APORTES", RecursoExportacion.Aportes)]
    [InlineData("evaluaciones", RecursoExportacion.Evaluaciones)]
    public void LeerRecurso_AceptaLosTresRecursosYElDefault(string valor, RecursoExportacion esperado)
        => ExportacionResultados.LeerRecurso(valor).Should().Be(esperado);

    [Fact]
    public void LeerParametros_ValoresInvalidos_Fallan_ConElCampoQueLosCausa()
    {
        var recurso = () => ExportacionResultados.LeerRecurso("todo");
        var formato = () => ExportacionResultados.LeerFormato("pdf");
        var anonimizado = () => ExportacionResultados.LeerAnonimizado("quizas");

        recurso.Should().Throw<ErrorValidacion>()
            .Which.Detalles.Should().ContainSingle(d => d.Campo == "recurso" && d.Problema == "valor_invalido");
        formato.Should().Throw<ErrorValidacion>()
            .Which.Detalles.Should().ContainSingle(d => d.Campo == "formato" && d.Problema == "valor_invalido");
        anonimizado.Should().Throw<ErrorValidacion>()
            .Which.Detalles.Should().ContainSingle(d => d.Campo == "anonimizado" && d.Problema == "valor_invalido");
    }

    // El tope protege de un archivo que nadie va a abrir; el mensaje dice cuántas filas hay.
    [Fact]
    public void VerificarTope_PorEncimaDelTope_Falla_YDiceElTotal()
    {
        var accion = () => ExportacionResultados.VerificarTope(ExportacionResultados.TopeFilas + 1);

        var error = accion.Should().Throw<ErrorValidacion>().Which;
        error.Detalles.Should().ContainSingle(d => d.Campo == "recurso" && d.Problema == "excede_tope");
        error.Message.Should().Contain("10001").And.Contain("10000");
        ExportacionResultados.VerificarTope(ExportacionResultados.TopeFilas);
    }

    [Fact]
    public void NombreArchivo_LlevaCampaniaRecursoYFecha_SinCaracteresQueRompanLaDescarga()
    {
        var nombre = ExportacionResultados.NombreArchivo(
            "Convención GHT / 2026", RecursoExportacion.Ideas, FormatoExportacion.Xlsx, Epoca);

        nombre.Should().Be("Convencion-GHT-2026_ideas_1970-01-01.xlsx");
    }

    [Fact]
    public void NombreDocumento_UsaCodigoYNombre_YSoloElCodigoSiEsAnonimizado()
    {
        var idea = Idea("idea_1", "u_ana", ideaIndice: 2);

        ExportacionResultados.NombreDocumento(idea, Ana(), anonimizado: false)
            .Should().Be("U-000042_Ana-Perez_idea-2.md");
        ExportacionResultados.NombreDocumento(idea, Ana(), anonimizado: true)
            .Should().Be("U-000042_idea-2.md");
    }

    [Fact]
    public void ConstruirIdeas_LlevaIdentidadTextoVigenteCalificacionYConteos()
    {
        var idea = Idea("idea_1", "u_ana").ConfirmarVersion("idea_1_v2", Epoca);
        var versiones = new[]
        {
            Version("idea_1_v1", "idea_1", 1, null, "Primera", ["resp_1"], EstadoConfirmacionVersionIdea.Descartada),
            Version("idea_1_v2", "idea_1", 2, "idea_1_v1", "Riego por goteo", ["resp_1", "resp_2"], EstadoConfirmacionVersionIdea.Confirmada),
        };

        var tabla = ExportacionResultados.ConstruirIdeas(
            [idea],
            Participantes(),
            new Dictionary<string, DominioEvaluacion>(StringComparer.Ordinal) { ["idea_1"] = Evaluacion() },
            versiones.ToLookup(version => version.IdeaId, StringComparer.Ordinal),
            anonimizado: false);

        var fila = tabla.Filas.Should().ContainSingle().Which;
        fila[0].Should().Be("Ana Perez");
        fila[1].Should().Be("U-000042");
        fila[2].Should().Be("Operaciones");
        fila[7].Should().Be("Riego por goteo");
        fila[8].Should().Be("sí");
        fila[10].Should().Be("4.5");
        fila[11].Should().Be("2");
        fila[12].Should().Be("2");
    }

    // D1: la casilla de anonimizado existe desde el primer día y no deja rastro del nombre.
    [Fact]
    public void ConstruirIdeas_Anonimizado_SustituyeElNombrePorElCodigo()
    {
        var tabla = ExportacionResultados.ConstruirIdeas(
            [Idea("idea_1", "u_ana")],
            Participantes(),
            new Dictionary<string, DominioEvaluacion>(StringComparer.Ordinal),
            Array.Empty<VersionIdeaConsolidada>().ToLookup(v => v.IdeaId, StringComparer.Ordinal),
            anonimizado: true);

        var fila = tabla.Filas.Should().ContainSingle().Which;
        fila.Should().NotContain("Ana Perez");
        fila[0].Should().Be("U-000042");
        fila[2].Should().Be("Operaciones");
    }

    [Fact]
    public void ConstruirIdeas_SinIdentidad_LoDiceEnVezDeDejarUnIdCrudo()
    {
        var tabla = ExportacionResultados.ConstruirIdeas(
            [Idea("idea_1", "u_borrado")],
            Participantes(),
            new Dictionary<string, DominioEvaluacion>(StringComparer.Ordinal),
            Array.Empty<VersionIdeaConsolidada>().ToLookup(v => v.IdeaId, StringComparer.Ordinal),
            anonimizado: false);

        tabla.Filas.Single()[0].Should().Be("Participante no identificado (u_borrado)");
    }

    [Fact]
    public void ConstruirAportes_UnaFilaPorMensaje_ConLaVersionQueLoIncorporo()
    {
        var idea = Idea("idea_1", "u_ana");
        var versiones = new[]
        {
            Version("idea_1_v1", "idea_1", 1, null, "Primera", ["resp_1"], EstadoConfirmacionVersionIdea.Descartada, ["resp_1"]),
            Version("idea_1_v2", "idea_1", 2, "idea_1_v1", "Segunda", ["resp_1", "resp_2"], EstadoConfirmacionVersionIdea.Confirmada, ["resp_2"]),
        };

        var tabla = ExportacionResultados.ConstruirAportes(
            [idea],
            Participantes(),
            [Aporte("resp_2", "idea_1", "Y regar de noche", Epoca.AddMinutes(10)), Aporte("resp_1", "idea_1", "Riego por goteo", Epoca), Aporte("resp_9", null, "Historico sin idea", Epoca)],
            versiones.ToLookup(version => version.IdeaId, StringComparer.Ordinal),
            anonimizado: false);

        // Orden cronológico y sin los aportes que no pertenecen al alcance filtrado.
        tabla.Filas.Select(fila => fila[6]).Should().Equal("Riego por goteo", "Y regar de noche");
        tabla.Filas[0][7].Should().Be("1");
        tabla.Filas[1][7].Should().Be("2");
    }

    [Fact]
    public void ConstruirEvaluaciones_LlevaCriteriosRubricaYModelo()
    {
        var tabla = ExportacionResultados.ConstruirEvaluaciones(
            [Idea("idea_1", "u_ana")],
            Participantes(),
            [Evaluacion(), Evaluacion(ideaId: "idea_fuera_de_alcance")],
            anonimizado: false);

        var fila = tabla.Filas.Should().ContainSingle().Which;
        fila[5].Should().Be("4.5");
        fila[6].Should().Be("claridad=4");
        fila[7].Should().Be("rub_1");
        fila[8].Should().Be("1");
        fila[9].Should().Be("cerrar");
        fila[11].Should().Be("gpt-4o-mini");
    }

    [Fact]
    public void ConstruirHojaFiltros_DeclaraAlcanceFechaYQuienExporto()
    {
        var hoja = ExportacionResultados.ConstruirHojaFiltros(
            "Convención GHT 2026",
            RecursoExportacion.Ideas,
            anonimizado: true,
            [("area", "Operaciones"), ("estadoResultado", "madura")],
            totalFilas: 12,
            Epoca,
            "Admin GHT (u_admin)");

        var lineas = hoja.Lineas.ToDictionary(linea => linea.Clave, linea => linea.Valor);
        lineas["Campaña"].Should().Be("Convención GHT 2026");
        lineas["Anonimizado"].Should().Be("sí");
        lineas["Filtro · area"].Should().Be("Operaciones");
        lineas["Total de filas"].Should().Be("12");
        lineas["Exportado por"].Should().Be("Admin GHT (u_admin)");
    }

    [Fact]
    public void ConstruirHojaFiltros_SinFiltros_LoDiceExplicitamente()
    {
        var hoja = ExportacionResultados.ConstruirHojaFiltros(
            "Campaña", RecursoExportacion.Ideas, false, [], 3, Epoca, "Admin");

        hoja.Lineas.Should().Contain(linea => linea.Valor == "sin filtros: la campaña completa");
    }

    private static IdeaConsolidada Idea(string id, string usuarioId, int ideaIndice = 1)
        => IdeaConsolidada.Crear(id, "c_1", usuarioId, "p_1", "conv_1", "resp_1", ideaIndice, Epoca);

    private static VersionIdeaConsolidada Version(
        string id,
        string ideaId,
        int numero,
        string? anterior,
        string texto,
        string[] acumulados,
        EstadoConfirmacionVersionIdea estado,
        string[]? nuevos = null)
        => VersionIdeaConsolidada.Crear(
            id, "c_1", ideaId, numero, anterior, texto, acumulados, nuevos ?? acumulados,
            TipoAporteIdea.Inicial, estado, null, null, null, null, Epoca,
            estado == EstadoConfirmacionVersionIdea.Confirmada ? Epoca : null);

    private static Respuesta Aporte(string id, string? ideaId, string texto, DateTimeOffset fecha)
        => Respuesta.Crear(
            id, "c_1", "u_ana", "p_1", "conv_1", texto, "whatsapp", false, EstadoRespuesta.Evaluada, fecha, null,
            ideaId: ideaId,
            tipoAporte: ideaId is null ? null : TipoAporteIdea.Inicial);

    private static DominioEvaluacion Evaluacion(string ideaId = "idea_1")
        => DominioEvaluacion.Crear(
            $"eval_{ideaId}", "c_1", "resp_1", "u_ana", "p_1", "rub_1", 1, "pr_1", 1, "llm_1",
            new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
            new Dictionary<string, decimal> { ["claridad"] = 1m },
            [CalificacionCriterio.Crear("claridad", 4m, "clara")],
            4.5m, "explica", "Buena idea", RecomendacionEvaluacion.Cerrar, null, ["riego"], ["agua"], false, Epoca,
            ideaId: ideaId);

    private static IReadOnlyDictionary<string, Usuario> Participantes()
        => new Dictionary<string, Usuario>(StringComparer.Ordinal) { ["u_ana"] = Ana() };

    private static Usuario Ana()
        => Usuario.Crear(
            "u_ana", 42, "Ana Perez", NumeroWhatsApp.FromNormalized("573001112233"), RolUsuario.Participante,
            EstadoRegistro.Activo, "Operaciones", "Flores El Aljibe", null, null, Epoca, Epoca, sede: "AL");
}
