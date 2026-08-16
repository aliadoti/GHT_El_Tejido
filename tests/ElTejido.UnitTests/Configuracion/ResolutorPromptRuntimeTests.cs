using ElTejido.Application.Configuracion;
using ElTejido.Domain.Configuracion;
using FluentAssertions;

namespace ElTejido.UnitTests.Configuracion;

/// <summary>
/// DT-I20-02 §5.4: runtime usa la versión más nueva que sea a la vez <b>activa y aprobada</b>. Antes
/// se tomaba la mayor por número y solo después se miraba su estado, así que inactivar la última
/// dejaba la familia sin prompt en vez de volver a la anterior: el rollback del runbook no era
/// confiable.
/// </summary>
public sealed class ResolutorPromptRuntimeTests
{
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    [Fact]
    public void V2Inactiva_RuntimeVuelveALaV1ActivaYAprobada()
    {
        var resolucion = ResolutorPromptRuntime.Resolver(
            [Version(1, EstadoPrompt.Activo, aprobado: true), Version(2, EstadoPrompt.Inactivo, aprobado: true)]);

        resolucion.Prompt!.Version.Should().Be(1);
        resolucion.Motivo.Should().BeNull();
    }

    [Fact]
    public void V2Borrador_RuntimeVuelveALaV1ActivaYAprobada()
    {
        var resolucion = ResolutorPromptRuntime.Resolver(
            [Version(1, EstadoPrompt.Activo, aprobado: true), Version(2, EstadoPrompt.Borrador, aprobado: false)]);

        resolucion.Prompt!.Version.Should().Be(1);
    }

    [Fact]
    public void V2ActivaSinAprobar_NoDesplazaALaV1Vigente()
    {
        // Activar sin aprobar tampoco es una publicación: runtime exige las dos condiciones.
        var resolucion = ResolutorPromptRuntime.Resolver(
            [Version(1, EstadoPrompt.Activo, aprobado: true), Version(2, EstadoPrompt.Activo, aprobado: false)]);

        resolucion.Prompt!.Version.Should().Be(1);
    }

    [Fact]
    public void V2ActivaYAprobada_RuntimeAvanzaALaMasNueva()
    {
        var resolucion = ResolutorPromptRuntime.Resolver(
            [Version(1, EstadoPrompt.Activo, aprobado: true), Version(2, EstadoPrompt.Activo, aprobado: true)]);

        resolucion.Prompt!.Version.Should().Be(2);
    }

    [Fact]
    public void ElOrdenDeEntradaNoImporta()
    {
        var resolucion = ResolutorPromptRuntime.Resolver(
            [Version(2, EstadoPrompt.Activo, aprobado: true), Version(3, EstadoPrompt.Inactivo, aprobado: true), Version(1, EstadoPrompt.Activo, aprobado: true)]);

        resolucion.Prompt!.Version.Should().Be(2);
    }

    [Theory]
    [InlineData(EstadoPrompt.Inactivo, true, ResolucionPromptRuntime.MotivoNoActivo)]
    [InlineData(EstadoPrompt.Borrador, false, ResolucionPromptRuntime.MotivoNoActivo)]
    [InlineData(EstadoPrompt.Activo, false, ResolucionPromptRuntime.MotivoNoAprobado)]
    public void SinNingunaVigente_DiagnosticaSegunLaVersionMasNueva(
        EstadoPrompt estado,
        bool aprobado,
        string motivo)
    {
        var resolucion = ResolutorPromptRuntime.Resolver([Version(1, estado, aprobado)]);

        resolucion.Prompt.Should().BeNull();
        resolucion.Motivo.Should().Be(motivo);
    }

    [Fact]
    public void SinVersiones_DiagnosticaFamiliaInexistente()
    {
        ResolutorPromptRuntime.Resolver(Array.Empty<Prompt>())
            .Motivo.Should().Be(ResolucionPromptRuntime.MotivoNoEncontrado);
        ResolutorPromptRuntime.Resolver(null)
            .Motivo.Should().Be(ResolucionPromptRuntime.MotivoNoEncontrado);
    }

    [Fact]
    public void VigenciaExigeEstadoActivoYAprobacionCompleta()
    {
        Version(1, EstadoPrompt.Activo, aprobado: true).EsVigenteParaRuntime.Should().BeTrue();
        Version(1, EstadoPrompt.Activo, aprobado: false).EsVigenteParaRuntime.Should().BeFalse();
        Version(1, EstadoPrompt.Inactivo, aprobado: true).EsVigenteParaRuntime.Should().BeFalse();
        Version(1, EstadoPrompt.Borrador, aprobado: false).EsVigenteParaRuntime.Should().BeFalse();
    }

    private static Prompt Version(int version, EstadoPrompt estado, bool aprobado)
        => Prompt.Crear(
            "pr_eval", "Prompt evaluacion", "evaluar", "Eres un evaluador.", version, estado,
            aprobado ? "u_admin" : null, aprobado ? Epoca : null, Epoca, Epoca);
}
