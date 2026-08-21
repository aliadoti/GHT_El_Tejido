using ElTejido.Application.Common;
using ElTejido.Application.Respuestas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Usuarios;
using FluentAssertions;

namespace ElTejido.UnitTests.Respuestas;

/// <summary>
/// P-34 §4.1/§4.2 (04 §5.8): interpretación y aplicación de los filtros y el orden del listado de
/// resultados. Es lógica pura, así que se prueba sin HTTP ni repositorios.
/// </summary>
public sealed class ConsultaIdeasResultadosTests
{
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    [Fact]
    public void Interpretar_SinParametros_DevuelveCriteriosVacios()
    {
        var criterios = ConsultaIdeasResultados.Interpretar(new CriteriosIdeasCrudos());

        criterios.Should().Be(CriteriosIdeas.Vacios);
        criterios.NecesitaTexto.Should().BeFalse();
        criterios.NecesitaCalificacion.Should().BeFalse();
    }

    // Un rango mal escrito no es «no hay resultados»: se rechaza y se dicen todos los motivos juntos.
    [Fact]
    public void Interpretar_ValoresInvalidos_ReportaTodosLosMotivos()
    {
        var accion = () => ConsultaIdeasResultados.Interpretar(new CriteriosIdeasCrudos(
            Desde: "ayer",
            CalificacionMin: "muy alta",
            Confirmada: "quizas",
            Orden: "porElNombreDelPerro",
            Dir: "arriba"));

        var error = accion.Should().Throw<ErrorValidacion>().Which;
        error.Detalles.Select(detalle => (detalle.Campo, detalle.Problema)).Should().BeEquivalentTo(new[]
        {
            ("desde", "formato_invalido"),
            ("calificacionMin", "formato_invalido"),
            ("confirmada", "valor_invalido"),
            ("orden", "valor_invalido"),
            ("dir", "valor_invalido"),
        });
    }

    [Fact]
    public void Interpretar_RangosAlReves_SonInvalidos()
    {
        var fechas = () => ConsultaIdeasResultados.Interpretar(
            new CriteriosIdeasCrudos(Desde: "2026-08-10T00:00:00Z", Hasta: "2026-08-01T00:00:00Z"));
        var calificaciones = () => ConsultaIdeasResultados.Interpretar(
            new CriteriosIdeasCrudos(CalificacionMin: "5", CalificacionMax: "2"));

        fechas.Should().Throw<ErrorValidacion>()
            .Which.Detalles.Should().ContainSingle(d => d.Campo == "desde" && d.Problema == "rango_invalido");
        calificaciones.Should().Throw<ErrorValidacion>()
            .Which.Detalles.Should().ContainSingle(d => d.Campo == "calificacionMin" && d.Problema == "rango_invalido");
    }

    [Fact]
    public void Interpretar_ReconoceQueOrdenPorCalificacionNecesitaLeerla()
    {
        var criterios = ConsultaIdeasResultados.Interpretar(new CriteriosIdeasCrudos(Orden: "calificacion", Dir: "desc"));

        criterios.Orden.Should().Be(OrdenIdeasResultados.Calificacion);
        criterios.Descendente.Should().BeTrue();
        criterios.NecesitaCalificacion.Should().BeTrue();
        criterios.NecesitaTexto.Should().BeFalse();
    }

    // La búsqueda libre encuentra por nombre con o sin acentos, por código legible y por el texto.
    [Theory]
    [InlineData("perez", new[] { "idea_ana" })]
    [InlineData("PÉREZ", new[] { "idea_ana" })]
    [InlineData("42", new[] { "idea_ana" })]
    [InlineData("u-000042", new[] { "idea_ana" })]
    [InlineData("riego", new[] { "idea_beto" })]
    [InlineData("zzz", new string[0])]
    public void FiltrarYOrdenar_BusquedaLibre_MiraNombreCodigoYTexto(string busqueda, string[] esperadas)
    {
        var resultado = ConsultaIdeasResultados.FiltrarYOrdenar(
            new[] { Idea("idea_ana", "u_ana"), Idea("idea_beto", "u_beto") },
            ConsultaIdeasResultados.Interpretar(new CriteriosIdeasCrudos(Q: busqueda)),
            Participantes(),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["idea_ana"] = "Automatizar el inventario",
                ["idea_beto"] = "Un sistema de riego por goteo",
            },
            new Dictionary<string, decimal>(StringComparer.Ordinal));

        resultado.Select(idea => idea.Id).Should().BeEquivalentTo(esperadas);
    }

    [Fact]
    public void FiltrarYOrdenar_FiltroDeArea_ExcluyeAlParticipanteNoResuelto()
    {
        var resultado = ConsultaIdeasResultados.FiltrarYOrdenar(
            new[] { Idea("idea_ana", "u_ana"), Idea("idea_beto", "u_beto"), Idea("idea_fantasma", "u_borrado") },
            ConsultaIdeasResultados.Interpretar(new CriteriosIdeasCrudos(Area: "operaciones")),
            Participantes(),
            SinTextos,
            SinCalificaciones);

        // La comparación no distingue mayúsculas; sin identidad no se puede afirmar el área.
        resultado.Select(idea => idea.Id).Should().Equal("idea_ana");
    }

    [Fact]
    public void FiltrarYOrdenar_RangoDeCalificacion_DejaFueraLasIdeasSinEvaluacion()
    {
        var calificaciones = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["idea_ana"] = 4.5m,
            ["idea_beto"] = 2m,
        };

        var resultado = ConsultaIdeasResultados.FiltrarYOrdenar(
            new[] { Idea("idea_ana", "u_ana"), Idea("idea_beto", "u_beto"), Idea("idea_sin_evaluar", "u_ana") },
            ConsultaIdeasResultados.Interpretar(new CriteriosIdeasCrudos(CalificacionMin: "3")),
            Participantes(),
            SinTextos,
            calificaciones);

        resultado.Select(idea => idea.Id).Should().Equal("idea_ana");
    }

    [Fact]
    public void FiltrarYOrdenar_PorCalificacion_DejaLasIdeasSinEvaluacionAlFinalEnAmbasDirecciones()
    {
        var ideas = new[] { Idea("idea_ana", "u_ana"), Idea("idea_beto", "u_beto"), Idea("idea_sin_evaluar", "u_ana") };
        var calificaciones = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["idea_ana"] = 4.5m,
            ["idea_beto"] = 2m,
        };

        var ascendente = ConsultaIdeasResultados.FiltrarYOrdenar(
            ideas, Criterios("calificacion", "asc"), Participantes(), SinTextos, calificaciones);
        var descendente = ConsultaIdeasResultados.FiltrarYOrdenar(
            ideas, Criterios("calificacion", "desc"), Participantes(), SinTextos, calificaciones);

        ascendente.Select(idea => idea.Id).Should().Equal("idea_beto", "idea_ana", "idea_sin_evaluar");
        descendente.Select(idea => idea.Id).Should().Equal("idea_ana", "idea_beto", "idea_sin_evaluar");
    }

    [Fact]
    public void FiltrarYOrdenar_PorParticipante_UsaElNombreVisibleYDejaAlNoIdentificadoAlFinal()
    {
        var ideas = new[] { Idea("idea_beto", "u_beto"), Idea("idea_fantasma", "u_borrado"), Idea("idea_ana", "u_ana") };

        var ascendente = ConsultaIdeasResultados.FiltrarYOrdenar(
            ideas, Criterios("participante", "asc"), Participantes(), SinTextos, SinCalificaciones);
        var descendente = ConsultaIdeasResultados.FiltrarYOrdenar(
            ideas, Criterios("participante", "desc"), Participantes(), SinTextos, SinCalificaciones);

        ascendente.Select(idea => idea.Id).Should().Equal("idea_ana", "idea_beto", "idea_fantasma");
        descendente.Select(idea => idea.Id).Should().Equal("idea_beto", "idea_ana", "idea_fantasma");
    }

    // El orden pedido desempata por el orden natural de I-19, para que paginar sea estable.
    [Fact]
    public void FiltrarYOrdenar_ConEmpate_ConservaElOrdenNaturalComoDesempate()
    {
        var ideas = new[]
        {
            Idea("idea_p2", "u_ana", preguntaId: "p_2", ideaIndice: 1),
            Idea("idea_p1_b", "u_ana", preguntaId: "p_1", ideaIndice: 2),
            Idea("idea_p1_a", "u_ana", preguntaId: "p_1", ideaIndice: 1),
        };

        var resultado = ConsultaIdeasResultados.FiltrarYOrdenar(
            ideas, Criterios("participante", "asc"), Participantes(), SinTextos, SinCalificaciones);

        resultado.Select(idea => idea.Id).Should().Equal("idea_p1_a", "idea_p1_b", "idea_p2");
    }

    [Fact]
    public void FiltrarYOrdenar_Confirmada_SeparaLoConfirmadoDeLoPropuesto()
    {
        var confirmada = Idea("idea_confirmada", "u_ana").ConfirmarVersion("v_1", Epoca);
        var propuesta = Idea("idea_propuesta", "u_ana").ConPropuesta("v_2", Epoca);
        var ideas = new[] { confirmada, propuesta };

        var soloConfirmadas = ConsultaIdeasResultados.FiltrarYOrdenar(
            ideas,
            ConsultaIdeasResultados.Interpretar(new CriteriosIdeasCrudos(Confirmada: "true")),
            Participantes(),
            SinTextos,
            SinCalificaciones);
        var soloPendientes = ConsultaIdeasResultados.FiltrarYOrdenar(
            ideas,
            ConsultaIdeasResultados.Interpretar(new CriteriosIdeasCrudos(Confirmada: "false")),
            Participantes(),
            SinTextos,
            SinCalificaciones);

        soloConfirmadas.Select(idea => idea.Id).Should().Equal("idea_confirmada");
        soloPendientes.Select(idea => idea.Id).Should().Equal("idea_propuesta");
    }

    [Fact]
    public void FiltrarYOrdenar_RangoDeFechas_EsInclusivoSobreLaFechaDeCreacion()
    {
        var ideas = new[]
        {
            Idea("idea_vieja", "u_ana", creadaEn: Epoca),
            Idea("idea_limite", "u_ana", creadaEn: Epoca.AddDays(5)),
            Idea("idea_nueva", "u_ana", creadaEn: Epoca.AddDays(10)),
        };

        var resultado = ConsultaIdeasResultados.FiltrarYOrdenar(
            ideas,
            ConsultaIdeasResultados.Interpretar(new CriteriosIdeasCrudos(
                Desde: Epoca.AddDays(1).ToString("O"),
                Hasta: Epoca.AddDays(5).ToString("O"))),
            Participantes(),
            SinTextos,
            SinCalificaciones);

        resultado.Select(idea => idea.Id).Should().Equal("idea_limite");
    }

    private static CriteriosIdeas Criterios(string orden, string dir)
        => ConsultaIdeasResultados.Interpretar(new CriteriosIdeasCrudos(Orden: orden, Dir: dir));

    private static IReadOnlyDictionary<string, string> SinTextos { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, decimal> SinCalificaciones { get; } =
        new Dictionary<string, decimal>(StringComparer.Ordinal);

    private static IdeaConsolidada Idea(
        string id,
        string usuarioId,
        string preguntaId = "p_1",
        int ideaIndice = 1,
        DateTimeOffset? creadaEn = null)
        => IdeaConsolidada.Crear(
            id, "c_1", usuarioId, preguntaId, $"conv_{usuarioId}", $"resp_{id}", ideaIndice, creadaEn ?? Epoca);

    private static IReadOnlyDictionary<string, Usuario> Participantes()
        => new Dictionary<string, Usuario>(StringComparer.Ordinal)
        {
            ["u_ana"] = Usuario("u_ana", 42, "Ana Pérez", "Operaciones"),
            ["u_beto"] = Usuario("u_beto", 43, "Beto Ruiz", "Comercial"),
        };

    private static Usuario Usuario(string id, int codigo, string nombre, string area)
        => ElTejido.Domain.Usuarios.Usuario.Crear(
            id,
            codigo,
            nombre,
            NumeroWhatsApp.FromNormalized("57300111" + codigo.ToString("D4")),
            RolUsuario.Participante,
            EstadoRegistro.Activo,
            area,
            "Flores El Aljibe",
            null,
            null,
            Epoca,
            Epoca,
            sede: "AL");
}
