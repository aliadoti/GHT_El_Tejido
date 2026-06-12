Buttons — the primary action control (`Button`) and its icon-only sibling (`IconButton`).

```jsx
<Button variant="primary" size="md" onClick={save}>Enviar a firma</Button>
<Button variant="secondary" leadingIcon={<i data-lucide="download" />}>Exportar</Button>
<Button variant="ghost">Cancelar</Button>
<IconButton icon={<i data-lucide="more-horizontal" />} label="Más opciones" />
```

Variants: `primary` (charcoal ink — one per view), `secondary` (white, sand border), `ghost` (text only), `danger` (muted red, destructive). Sizes `sm | md | lg`. Pass `leadingIcon` / `trailingIcon` as React nodes — keep icons Lucide for stroke consistency.
