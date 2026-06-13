using ElTejido.Application.Auth;
using ElTejido.Domain.Identidad;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ElTejido.Infrastructure.Seguridad;

/// <summary>
/// Limite de solicitudes de OTP por numero con ventana fija en memoria (REQ §10.3.7, 10 §2).
/// Suficiente para el MVP en un solo proceso (02 §5); la ventana no se desliza al incrementar.
/// </summary>
public sealed class LimitadorOtpMemoria : ILimitadorOtp
{
    private const string PrefijoClave = "otp-solicitudes:";

    private readonly IMemoryCache _cache;
    private readonly int _maximo;
    private readonly TimeSpan _ventana;

    public LimitadorOtpMemoria(IMemoryCache cache, IOptions<OpcionesAuth> opciones)
    {
        _cache = cache;
        _maximo = Math.Max(1, opciones.Value.OtpSolicitudesPorVentana);
        _ventana = TimeSpan.FromMinutes(Math.Max(1, opciones.Value.OtpVentanaSolicitudesMinutos));
    }

    public Task<bool> RegistrarYPermitirAsync(NumeroWhatsApp numero, CancellationToken cancellationToken)
    {
        var clave = PrefijoClave + numero.Valor;
        var contador = _cache.GetOrCreate(clave, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _ventana;
            return new Contador();
        })!;

        var conteo = Interlocked.Increment(ref contador.Valor);
        return Task.FromResult(conteo <= _maximo);
    }

    private sealed class Contador
    {
        public int Valor;
    }
}
