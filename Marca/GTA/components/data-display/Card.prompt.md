Display primitives — `Card` (the fundamental surface), `Badge` (status pill), `Tag` (chip), `Avatar` (initials/image).

```jsx
<Card eyebrow="Fase 02" title="Revisión y aprobación" interactive footer={<Button>Ver detalle</Button>}>
  <p>Panel de administración con listado, filtros y devolución parcial de documentos.</p>
</Card>

<Badge tone="success" dot>Firmado</Badge>
<Badge tone="warning">En revisión</Badge>
<Tag onRemove={() => {}}>Cámara de Comercio</Tag>
<Avatar name="Jason Pérez" /> <Avatar name="GR Chía" size={32} />
```

Badge tones: `neutral | info | success | warning | danger`, `variant="soft|solid"`. Card lifts on hover when `interactive`.
