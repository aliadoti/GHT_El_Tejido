# Plan de implementación — DT-QA-03

1. Diseñar el contrato mínimo de captura y la autorización del modo QA sin fijar ni cambiar aún App
   Settings remotos.
2. Implementar el doble de `IWhatsAppGateway` y su selección explícita en DI, con fail-closed.
3. Exponer solo la evidencia diagnóstica necesaria para leer la salida simulada.
4. Cubrir integración completa y demostrar que el gateway real no se invoca en modo simulado.
5. Actualizar QAS/16–18 y ejecutar únicamente los casos P-32 antes BLOCKED cuando exista un despliegue
   autorizado del artefacto.

