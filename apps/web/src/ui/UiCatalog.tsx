import { useState } from 'react'
import {
  Button,
  Card,
  ConfirmDialog,
  EmptyState,
  Field,
  IconButton,
  Input,
  List,
  ListRow,
  Skeleton,
  ToastProvider,
  useToast,
} from './index'
import { tokens } from '../theme/tokens'

/**
 * Catálogo navegable de la biblioteca de componentes (PMB-1.2) — solo
 * desarrollo (`#ui-kit`). Sirve para revisar y capturar el sistema visual;
 * no es una pantalla del producto y no se enlaza desde la aplicación.
 */

function ToastDemo() {
  const toast = useToast()
  return (
    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
      <Button onClick={() => toast.show({ title: 'Guardado', text: 'Los cambios están aplicados.', tone: 'success' })}>
        Toast éxito
      </Button>
      <Button
        onClick={() => toast.show({ title: 'Error', text: 'No se pudo guardar.', tone: 'error' })}
      >
        Toast error (persistente)
      </Button>
    </div>
  )
}

export function UiCatalog() {
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [selected, setSelected] = useState(0)

  return (
    <ToastProvider>
      <div style={{ background: tokens.color.surface0, color: tokens.color.textPrimary, minHeight: '100vh', padding: 32, fontFamily: "'Inter', system-ui, sans-serif" }}>
        <h1 style={{ fontSize: 28, margin: '0 0 8px' }}>Catálogo de componentes — ProjectVTT</h1>
        <p style={{ color: tokens.color.textSecondary, marginTop: 0 }}>
          PMB-1.2 · docs/visual/COMPONENT_RULES.md · solo desarrollo
        </p>

        <h2 style={{ fontSize: 18, marginTop: 32 }}>Color</h2>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          {Object.entries(tokens.color).map(([name, value]) => (
            <div key={name} style={{ width: 120 }}>
              <div style={{ height: 48, background: value, borderRadius: 6, border: '1px solid #33383f' }} />
              <div style={{ fontSize: 11, color: tokens.color.textMuted }}>{name}</div>
            </div>
          ))}
        </div>

        <h2 style={{ fontSize: 18, marginTop: 32 }}>Tipografía</h2>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <span style={{ fontSize: 28, fontWeight: 600 }}>Título de pantalla (h1 · 28px)</span>
          <span style={{ fontSize: 22, fontWeight: 600 }}>Título de sección (h2 · 22px)</span>
          <span style={{ fontSize: 18, fontWeight: 600 }}>Título de panel (h3 · 18px)</span>
          <span style={{ fontSize: 15 }}>Texto de prosa (15px) para descripciones y lore.</span>
          <span style={{ fontSize: 13 }}>Interfaz por defecto (13px): la densidad compacta manda.</span>
          <span style={{ fontSize: 12, color: tokens.color.textSecondary }}>Metadatos (12px)</span>
          <span style={{ fontSize: 11, color: tokens.color.textMuted }}>Etiquetas y leyendas (11px)</span>
        </div>

        <h2 style={{ fontSize: 18, marginTop: 32 }}>Botones</h2>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
          <Button variant="primary">Crear campaña</Button>
          <Button variant="secondary">Secundaria</Button>
          <Button variant="ghost">Fantasma</Button>
          <Button variant="danger">Eliminar</Button>
          <Button variant="danger-solid">Confirmar borrado</Button>
          <Button variant="primary" disabled>Deshabilitado</Button>
          <IconButton label="Actualizar" icon={<span aria-hidden="true">⟳</span>} />
        </div>

        <h2 style={{ fontSize: 18, marginTop: 32 }}>Formularios</h2>
        <div style={{ display: 'grid', gap: 16, maxWidth: 360 }}>
          <Field label="Nombre de usuario" helper="3–32 caracteres">
            {({ id, ...aria }) => <Input id={id} placeholder="usuario" {...aria} />}
          </Field>
          <Field label="Nombre de la campaña" error="El nombre ya existe.">
            {({ id, ...aria }) => <Input id={id} defaultValue="Campaña de prueba" {...aria} />}
          </Field>
        </div>

        <h2 style={{ fontSize: 18, marginTop: 32 }}>Listas y tarjetas</h2>
        <div style={{ display: 'grid', gap: 16, maxWidth: 560 }}>
          <List>
            {['Escena del puerto', 'Bosque de Scaldar', 'Ciudadela interior'].map((name, index) => (
              <ListRow
                key={name}
                title={name}
                meta={`grid hex · ${index + 2} tokens`}
                selected={selected === index}
                onClick={() => setSelected(index)}
                actions={<IconButton label={`Opciones de ${name}`} icon={<span aria-hidden="true">⋯</span>} />}
              />
            ))}
          </List>
          <Card
            thumb={<span aria-hidden="true">⚔</span>}
            title="Espada larga de Scaldar"
            meta={<><span>Arma · Marcial</span><span>Rareza: común</span><span>1d8 cortante</span></>}
            actions={<Button variant="ghost">Editar</Button>}
          />
        </div>

        <h2 style={{ fontSize: 18, marginTop: 32 }}>Estados y feedback</h2>
        <div style={{ display: 'grid', gap: 16, maxWidth: 560 }}>
          <EmptyState
            icon={<span aria-hidden="true">🗺</span>}
            title="Todavía no hay escenas"
            text="Crea la primera escena para empezar a jugar."
            action={<Button variant="primary">Crear escena</Button>}
          />
          <div style={{ display: 'grid', gap: 8 }}>
            <Skeleton width={240} height={12} />
            <Skeleton width={180} height={12} />
            <Skeleton width={64} height={64} radius={6} />
          </div>
          <ToastDemo />
          <Button variant="danger" onClick={() => setConfirmOpen(true)}>
            Eliminar campaña (confirmación)
          </Button>
          <ConfirmDialog
            open={confirmOpen}
            title="¿Eliminar la campaña?"
            text="Esta acción no se puede deshacer. Se eliminarán las escenas, personajes y mapas asociados."
            confirmLabel="Eliminar"
            cancelLabel="Cancelar"
            destructive
            confirmPhrase="Camp de prueba"
            phraseLabel="Escribe el nombre de la campaña para confirmar"
            onConfirm={() => setConfirmOpen(false)}
            onCancel={() => setConfirmOpen(false)}
          />
        </div>
      </div>
    </ToastProvider>
  )
}
