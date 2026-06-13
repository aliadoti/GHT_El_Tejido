namespace ElTejido.Application.Auth;

/// <summary>
/// Datos publicos del usuario de la sesion devueltos al SPA (04 §4.2/§4.4).
/// </summary>
/// <param name="Id">Identificador del usuario.</param>
/// <param name="Nombre">Nombre para mostrar.</param>
/// <param name="Rol">Rol en minusculas (<c>admin</c>/<c>visor</c>/<c>participante</c>).</param>
public sealed record UsuarioSesion(string Id, string Nombre, string Rol);
