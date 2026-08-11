using ElTejido.Application.Common;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;

namespace ElTejido.Application.Campanas;

/// <summary>Reglas puras de completitud editorial antes de activar una campaña multidioma.</summary>
public static class ValidadorLocalizacionesCampania
{
    public static IReadOnlyList<DetalleError> Validar(Campania campania)
    {
        var errores = new List<DetalleError>();
        foreach (var idioma in campania.IdiomasHabilitados)
        {
            if (!campania.TryObtenerLocalizacion(idioma, out var localizacion))
            {
                errores.Add(new DetalleError($"localizaciones.{idioma}", "obligatoria"));
                continue;
            }

            Requerir(localizacion.Nombre, $"localizaciones.{idioma}.nombre", errores);
            Requerir(localizacion.Descripcion, $"localizaciones.{idioma}.descripcion", errores);
            Requerir(localizacion.Objetivo, $"localizaciones.{idioma}.objetivo", errores);
            Requerir(localizacion.MensajeCierre, $"localizaciones.{idioma}.mensajeCierre", errores);

            foreach (var mensaje in campania.MensajesIniciales.Where(mensaje => mensaje.Estado == EstadoRegistro.Activo))
            {
                if (!localizacion.MensajesIniciales.TryGetValue(mensaje.Id, out var mensajeLocalizado))
                {
                    errores.Add(new DetalleError($"localizaciones.{idioma}.mensajesIniciales.{mensaje.Id}", "obligatorio"));
                    continue;
                }

                Requerir(mensajeLocalizado.Texto, $"localizaciones.{idioma}.mensajesIniciales.{mensaje.Id}.texto", errores);
                Requerir(mensajeLocalizado.PlantillaRef, $"localizaciones.{idioma}.mensajesIniciales.{mensaje.Id}.plantillaRef", errores);
            }

            foreach (var pregunta in campania.Preguntas.Where(pregunta => pregunta.Estado == EstadoRegistro.Activo))
            {
                if (!localizacion.Preguntas.TryGetValue(pregunta.Id, out var preguntaLocalizada))
                {
                    errores.Add(new DetalleError($"localizaciones.{idioma}.preguntas.{pregunta.Id}", "obligatoria"));
                    continue;
                }

                Requerir(preguntaLocalizada.Texto, $"localizaciones.{idioma}.preguntas.{pregunta.Id}.texto", errores);
                Requerir(preguntaLocalizada.Instruccion, $"localizaciones.{idioma}.preguntas.{pregunta.Id}.instruccion", errores);
            }
        }

        return errores;
    }

    private static void Requerir(string? valor, string campo, ICollection<DetalleError> errores)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            errores.Add(new DetalleError(campo, "obligatorio"));
        }
    }
}
