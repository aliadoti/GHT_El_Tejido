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
}
