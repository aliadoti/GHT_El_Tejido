Form controls — `Input`, `Textarea`, `Select`, `Checkbox`, `Switch`. All share the cream surface, sand border, ink focus ring, and 700-weight labels.

```jsx
<Input label="NIT del proveedor" placeholder="900.123.456-7" hint="Sin dígito de verificación" />
<Select label="Tipo de persona" placeholder="Selecciona…" options={["Natural","Jurídica"]} />
<Input label="Correo" error="Ya existe un proveedor con este correo" />
<Checkbox label="Acepto los términos" defaultChecked />
<Switch label="Notificaciones por correo" defaultChecked />
```

Pass `error` to any field to turn its border red and show the message. Sizes `sm | md | lg` on Input/Select.
