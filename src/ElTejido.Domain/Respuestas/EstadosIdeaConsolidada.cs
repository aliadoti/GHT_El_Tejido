namespace ElTejido.Domain.Respuestas;

/// <summary>Estado operativo de una idea consolidada I-19.</summary>
public enum EstadoFlujoIdeaConsolidada
{
    PendienteConfirmacion,
    EnMejora,
    EnRevision,
    Cerrada,
}

/// <summary>Resultado de la idea; solo se asigna al terminar una evaluación o un cierre explícito.</summary>
public enum EstadoResultadoIdeaConsolidada
{
    Madura,
    Pendiente,
    Rechazada,
}

/// <summary>Estado futuro de curaduría, separado del resultado automático de I-19.</summary>
public enum EstadoCuraduriaIdea
{
    Pendiente,
}

/// <summary>Origen auditable del aporte original de una idea.</summary>
public enum TipoAporteIdea
{
    Inicial,
    Complemento,
    Correccion,
    NuevaIdea,
}

/// <summary>Confirmación de una versión consolidada inmutable.</summary>
public enum EstadoConfirmacionVersionIdea
{
    Propuesta,
    Confirmada,
    Descartada,
    Expirada,
}
