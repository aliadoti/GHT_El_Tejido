Navigation — `Tabs` (underline) and `Stepper` (multi-step progress, e.g. the 9-step inscription wizard).

```jsx
<Tabs defaultValue="solicitudes" tabs={[
  { id: "solicitudes", label: "Solicitudes", count: 12 },
  { id: "areas", label: "Áreas" },
  { id: "ans", label: "ANS y reportes" },
]} onChange={setTab} />

<Stepper current={2} steps={["Identificación","Tipo","Datos","Documentos","Firma"]} />
```

Tabs: pass `value`/`onChange` for controlled. Stepper: steps before `current` show a check; `current` is filled ink.
