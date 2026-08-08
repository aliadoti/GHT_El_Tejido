namespace ElTejido.Application.Usuarios.CargaMasiva;

/// <summary>
/// Genera la plantilla vacia que el admin descarga desde el portal (I-08 §4.5, 04 §5.1). Se construye
/// en caliente desde <see cref="PlantillaParticipantes.Cabecera"/> en vez de servir un archivo del
/// repositorio, para que la plantilla y el lector no puedan desincronizarse.
/// </summary>
public interface IGeneradorPlantillaParticipantes
{
    /// <summary>Nombre sugerido del archivo descargado.</summary>
    string NombreArchivo { get; }

    /// <summary>Content-type del archivo generado.</summary>
    string TipoContenido { get; }

    byte[] Generar();
}
