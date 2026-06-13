using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Seguridad;
using FluentAssertions;

namespace ElTejido.UnitTests.Seguridad;

public sealed class CodigoAuthAdminTests
{
    private static readonly DateTimeOffset Creado = new(2026, 6, 12, 15, 4, 0, TimeSpan.Zero);

    [Fact]
    public void EsVigente_TrueCuandoNoUsadoConIntentosYNoExpirado()
    {
        var codigo = Crear();

        codigo.EsVigente(Creado.AddMinutes(1)).Should().BeTrue();
    }

    [Fact]
    public void EsVigente_FalseCuandoExpirado()
    {
        var codigo = Crear();

        codigo.EsVigente(Creado.AddMinutes(6)).Should().BeFalse();
    }

    [Fact]
    public void ConIntentoConsumido_DecrementaSinBajarDeCero()
    {
        var codigo = Crear(intentos: 1);

        var primero = codigo.ConIntentoConsumido();
        var segundo = primero.ConIntentoConsumido();

        primero.IntentosRestantes.Should().Be(0);
        segundo.IntentosRestantes.Should().Be(0);
        segundo.EsVigente(Creado.AddMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void ComoUsado_MarcaUsadoYNoVigente()
    {
        var codigo = Crear().ComoUsado();

        codigo.Usado.Should().BeTrue();
        codigo.EsVigente(Creado.AddMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void Crear_RechazaExpiracionNoPosterior()
    {
        var act = () => CodigoAuthAdmin.Crear(
            "otp_1",
            "u_admin1",
            NumeroWhatsApp.FromNormalized("573001119999"),
            "$hash$",
            Creado,
            5,
            false,
            Creado,
            600);

        act.Should().Throw<DomainValidationException>().Which.Code.Should().Be("EXPIRACION_INVALIDA");
    }

    private static CodigoAuthAdmin Crear(int intentos = 5)
    {
        return CodigoAuthAdmin.Crear(
            "otp_1",
            "u_admin1",
            NumeroWhatsApp.FromNormalized("573001119999"),
            "$hash$",
            Creado.AddMinutes(5),
            intentos,
            false,
            Creado,
            600);
    }
}
