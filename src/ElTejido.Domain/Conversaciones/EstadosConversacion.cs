namespace ElTejido.Domain.Conversaciones;

/// <summary>Estado de vida de una conversacion (03 §3.6).</summary>
public enum EstadoConversacion
{
    Abierta,
    Cerrada,
}

/// <summary>
/// Estado de la maquina de repregunta (03 §3.6, 05 §4.2). Controla el unico turno de repregunta del
/// MVP: <c>esperandoRespuestaInicial → evaluando → (esperandoRepregunta → evaluando) → cerrada</c>.
/// </summary>
public enum EstadoMaquinaConversacion
{
    EsperandoRespuestaInicial,
    Evaluando,
    EsperandoRepregunta,
    Cerrada,
}

/// <summary>Direccion de un mensaje del hilo (03 §3.7).</summary>
public enum DireccionMensaje
{
    In,
    Out,
}
