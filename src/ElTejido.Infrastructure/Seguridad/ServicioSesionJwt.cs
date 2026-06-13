using System.Security.Cryptography;
using System.Text;
using ElTejido.Application.Auth;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Usuarios;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ElTejido.Infrastructure.Seguridad;

/// <summary>
/// Emite y valida la sesion admin como JWT corto HS256 firmado con <c>jwt-sign</c> de Key Vault
/// (06 §4.3b, 10 §5). Incluye <c>sub</c> (usuarioId), <c>name</c>, <c>role</c> y un token CSRF.
/// No hay contenedor de sesion: la invalidacion depende de la expiracion corta.
/// </summary>
public sealed class ServicioSesionJwt : IServicioSesion
{
    private const string Emisor = "eltejido";
    private const string Audiencia = "eltejido-admin";
    private const string ClaimRol = "role";
    private const string ClaimCsrf = "csrf";

    private static readonly JsonWebTokenHandler Handler = new();

    private readonly ISecretProvider _secretos;
    private readonly OpcionesAuth _opciones;
    private readonly TimeProvider _tiempo;

    public ServicioSesionJwt(ISecretProvider secretos, IOptions<OpcionesAuth> opciones, TimeProvider tiempo)
    {
        _secretos = secretos;
        _opciones = opciones.Value;
        _tiempo = tiempo;
    }

    public async Task<SesionEmitida> EmitirAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        var clave = await ObtenerClaveAsync(cancellationToken);
        var ahora = _tiempo.GetUtcNow();
        var expira = ahora.AddMinutes(_opciones.SesionTtlMinutos);
        var csrf = GenerarCsrf();
        var rol = usuario.Rol.ToString().ToLowerInvariant();

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Emisor,
            Audience = Audiencia,
            IssuedAt = ahora.UtcDateTime,
            NotBefore = ahora.UtcDateTime,
            Expires = expira.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = usuario.Id,
                [JwtRegisteredClaimNames.Name] = usuario.Nombre,
                [ClaimRol] = rol,
                [ClaimCsrf] = csrf,
            },
            SigningCredentials = new SigningCredentials(clave, SecurityAlgorithms.HmacSha256),
        };

        var token = Handler.CreateToken(descriptor);
        return new SesionEmitida(token, csrf, expira, new UsuarioSesion(usuario.Id, usuario.Nombre, rol));
    }

    public async Task<PrincipalSesion?> ValidarAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var clave = await ObtenerClaveAsync(cancellationToken);
        var parametros = new TokenValidationParameters
        {
            ValidIssuer = Emisor,
            ValidAudience = Audiencia,
            IssuerSigningKey = clave,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        var resultado = await Handler.ValidateTokenAsync(token, parametros);
        if (!resultado.IsValid)
        {
            return null;
        }

        var usuarioId = LeerClaim(resultado, JwtRegisteredClaimNames.Sub);
        var nombre = LeerClaim(resultado, JwtRegisteredClaimNames.Name);
        var rolTexto = LeerClaim(resultado, ClaimRol);
        var csrf = LeerClaim(resultado, ClaimCsrf);

        if (usuarioId is null || nombre is null || rolTexto is null || csrf is null
            || !Enum.TryParse<RolUsuario>(rolTexto, ignoreCase: true, out var rol))
        {
            return null;
        }

        var expira = resultado.SecurityToken is JsonWebToken jwt
            ? new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero)
            : _tiempo.GetUtcNow();

        return new PrincipalSesion(usuarioId, nombre, rol, csrf, expira);
    }

    private static string? LeerClaim(TokenValidationResult resultado, string tipo)
        => resultado.Claims.TryGetValue(tipo, out var valor) ? valor?.ToString() : null;

    private async Task<SymmetricSecurityKey> ObtenerClaveAsync(CancellationToken cancellationToken)
    {
        var secreto = await _secretos.ObtenerSecretoAsync(NombresSecretos.JwtSign, cancellationToken);
        var bytes = Encoding.UTF8.GetBytes(secreto);
        if (bytes.Length < 32)
        {
            throw new InvalidOperationException(
                "La clave de firma de sesion (jwt-sign) debe tener al menos 32 bytes para HS256.");
        }

        return new SymmetricSecurityKey(bytes);
    }

    private static string GenerarCsrf() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
