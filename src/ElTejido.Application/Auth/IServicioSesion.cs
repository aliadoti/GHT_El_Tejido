using ElTejido.Domain.Usuarios;

namespace ElTejido.Application.Auth;

/// <summary>
/// Emite y valida sesiones admin como token firmado (JWT corto HS256 con la clave <c>jwt-sign</c>
/// de Key Vault; 06 §4.3b, 10 §5). No hay contenedor de sesion: la invalidacion depende de la
/// expiracion corta.
/// </summary>
public interface IServicioSesion
{
    Task<SesionEmitida> EmitirAsync(Usuario usuario, CancellationToken cancellationToken);

    /// <summary>Valida la firma y vigencia del token; devuelve la identidad o <c>null</c> si es invalido.</summary>
    Task<PrincipalSesion?> ValidarAsync(string token, CancellationToken cancellationToken);
}
