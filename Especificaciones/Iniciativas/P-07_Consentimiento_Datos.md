# P-07 — Bienvenida/consentimiento y manejo de datos

> **Origen:** hoja `Iniciativas` (propuesta Aliado TI). **Tipo:** Config/Prompt + Desarrollo
> aditivo · **Prioridad:** Media (requisito de cumplimiento antes de participantes reales) ·
> **Ventana:** Sprint 2 · **Dependencia:** copy legal de GHT · **Riesgo:** bajo técnico, medio de
> cumplimiento. Cubre REQ §10/§19, ARQ §10; specs base `03 §3.4/§3.6`.

## 1. Qué pide / por qué
El arranque debe explicar **qué es la herramienta y cómo se usan los datos** antes de abrir a
participantes reales. Relevante porque I-09 comparte aportes entre participantes.

## 2. Diseño técnico
1. **Aviso en el arranque (sin código):** el `MensajeInicial` de la campaña de convención incluye
   el aviso de privacidad/consentimiento (texto configurable por el portal; copy legal con GHT).
2. **Registro del consentimiento (código aditivo):** campo `consentimientoAceptadoEn`
   (timestamp opcional) en `ParticipanteCampania`, sellado al **primer entrante** del participante
   (respondió después de recibir el aviso = consentimiento implícito de participación; decisión
   final del mecanismo con GHT). Contrato `03 §3.4` aditivo — **spec en commit aparte**.
3. **Regla para el tejido (I-09):** solo se recuperan aportes de participantes con
   `consentimientoAceptadoEn` sellado y de campañas cuyo arranque declaró el uso colectivo;
   anonimizado por defecto.
4. **Retención/backup (T-37):** revisar con GHT la política de retención de Markdown y logs; queda
   documentada en `SUPUESTOS.md` (no requiere código en el Hito).

## 3. Criterios de aceptación / pruebas
- Unit: el primer entrante sella `consentimientoAceptadoEn`; entrantes posteriores no lo cambian.
- Unit (I-09): participante sin consentimiento → sus aportes no se recuperan para el tejido.
- El primer mensaje de la campaña real contiene el aviso acordado; decisión registrada en `SUPUESTOS.md`.
- Spec `03` actualizada en commit aparte; build/test/format verdes.
