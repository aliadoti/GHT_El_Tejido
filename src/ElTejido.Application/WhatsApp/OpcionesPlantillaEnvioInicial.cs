namespace ElTejido.Application.WhatsApp;

/// <summary>
/// Configuracion no secreta de la plantilla HSM aprobada para iniciar campanias por WhatsApp
/// (05 seccion 2.2). Se carga desde <c>WhatsApp:PlantillaEnvioInicial</c> para que el App Service pueda
/// cambiar la plantilla sin redeploy.
/// </summary>
public sealed class OpcionesPlantillaEnvioInicial
{
    public const string Seccion = "WhatsApp:PlantillaEnvioInicial";

    public string Nombre { get; set; } = string.Empty;

    public string Idioma { get; set; } = "es_CO";

    public string[] Componentes { get; set; } = [];

    /// <summary>
    /// Mapa por ambiente de alias lógicos de campaña a plantillas Meta aprobadas. Los textos y el
    /// alias viven en Cosmos; los identificadores físicos de Meta permanecen en App Settings.
    /// Ejemplo: <c>Mapeos:inicio_campania:en:Nombre</c>.
    /// </summary>
    public Dictionary<string, Dictionary<string, PlantillaEnvioInicialConfigurada>> Mapeos { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);

}

public sealed class PlantillaEnvioInicialConfigurada
{
    public string Nombre { get; set; } = string.Empty;
    public string Idioma { get; set; } = string.Empty;
    public string[] Componentes { get; set; } = [];
}
