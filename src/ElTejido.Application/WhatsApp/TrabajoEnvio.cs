using ElTejido.Domain.Campanas;
using ElTejido.Domain.Participantes;

namespace ElTejido.Application.WhatsApp;

/// <summary>
/// Unidad de trabajo de un envio saliente encolado (05 §2.5). Lleva todo lo necesario para enviar
/// sin recargar la campania por item: numero, plantilla/variables ya resueltas y texto renderizado
/// para flujos que puedan enviar texto libre dentro de ventana. Pertenece a un <see cref="JobEnvio"/>.
/// </summary>
public sealed record TrabajoEnvio(
    string JobId,
    string CampaniaId,
    string UsuarioId,
    string Numero,
    string MensajeInicialId,
    PlantillaWhatsApp? Plantilla,
    IReadOnlyDictionary<string, string> Variables,
    string TextoLibre,
    TipoEnvioMensaje Tipo);
