import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  Badge,
  Button,
  Card,
  ConfirmDialog,
  EmptyState,
  Field,
  FilterChip,
  IconButton,
  Input,
  List,
  ListRow,
  Skeleton,
  ToastProvider,
  useToast,
} from './index'

// Vitest no usa globals: sin auto-limpieza de RTL, los renderizados se
// acumularían entre tests del mismo fichero.
afterEach(cleanup)

describe('Button', () => {
  it('renderiza la etiqueta y dispara onClick', () => {
    const onClick = vi.fn()
    render(<Button variant="primary" onClick={onClick}>Crear</Button>)
    fireEvent.click(screen.getByRole('button', { name: 'Crear' }))
    expect(onClick).toHaveBeenCalledTimes(1)
  })

  it('respeta disabled sin disparar onClick', () => {
    const onClick = vi.fn()
    render(<Button disabled onClick={onClick}>Guardar</Button>)
    fireEvent.click(screen.getByRole('button', { name: 'Guardar' }))
    expect(onClick).not.toHaveBeenCalled()
  })

  it('IconButton exige etiqueta accesible', () => {
    render(<IconButton label="Actualizar" icon={<span>*</span>} />)
    expect(screen.getByRole('button', { name: 'Actualizar' })).toBeTruthy()
  })
})

describe('Field e Input', () => {
  it('asocia etiqueta, ayuda y control', () => {
    render(
      <Field label="Nombre" helper="3–32 caracteres">
        {({ id, ...aria }) => <Input id={id} placeholder="usuario" {...aria} />}
      </Field>,
    )
    const input = screen.getByLabelText('Nombre') as HTMLInputElement
    expect(input.placeholder).toBe('usuario')
    expect(input.getAttribute('aria-invalid')).toBe('false')
    expect(screen.getByText('3–32 caracteres')).toBeTruthy()
  })

  it('muestra el error y marca aria-invalid', () => {
    render(
      <Field label="Nombre" error="El nombre ya existe.">
        {({ id, ...aria }) => <Input id={id} {...aria} />}
      </Field>,
    )
    expect(screen.getByLabelText('Nombre').getAttribute('aria-invalid')).toBe('true')
    expect(screen.getByText('El nombre ya existe.')).toBeTruthy()
  })
})

describe('List y Card', () => {
  it('la fila seleccionada se marca y dispara onClick', () => {
    const onClick = vi.fn()
    render(
      <List>
        <ListRow title="Escena 1" meta="2 tokens" selected onClick={onClick} />
      </List>,
    )
    const row = screen.getByRole('button', { name: /Escena 1/ })
    expect(row.getAttribute('aria-current')).toBe('true')
    fireEvent.click(row)
    expect(onClick).toHaveBeenCalledTimes(1)
  })

  it('Card muestra título, metadatos y acciones', () => {
    render(
      <Card title="Espada larga" meta={<span>Arma</span>} actions={<Button>Editar</Button>} />,
    )
    expect(screen.getByText('Espada larga')).toBeTruthy()
    expect(screen.getByText('Arma')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Editar' })).toBeTruthy()
  })
})

describe('EmptyState y Skeleton', () => {
  it('EmptyState guía con título, texto y acción', () => {
    render(
      <EmptyState title="Sin escenas" text="Crea la primera." action={<Button variant="primary">Crear</Button>} />,
    )
    expect(screen.getByText('Sin escenas')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Crear' })).toBeTruthy()
  })

  it('Skeleton no anuncia nada a lectores de pantalla', () => {
    const { container } = render(<Skeleton width={100} height={12} />)
    expect(container.querySelector('.pv-skeleton')?.getAttribute('aria-hidden')).toBe('true')
  })
})

describe('Badge y FilterChip', () => {
  it('Badge aplica su tono semántico', () => {
    render(<Badge tone="warning">En pausa</Badge>)
    expect(screen.getByText('En pausa').className).toContain('pv-badge--warning')
  })

  it('FilterChip expone el estado seleccionado y dispara onClick', () => {
    const onClick = vi.fn()
    render(<FilterChip label="Propias" selected onClick={onClick} />)
    const chip = screen.getByRole('button', { name: 'Propias' })
    expect(chip.getAttribute('aria-pressed')).toBe('true')
    fireEvent.click(chip)
    expect(onClick).toHaveBeenCalledTimes(1)
  })
})

function ToastHarness() {
  const toast = useToast()
  return (
    <Button onClick={() => toast.show({ title: 'Guardado', tone: 'success' })}>Avisar</Button>
  )
}

describe('Toasts', () => {
  it('muestra y permite cerrar el aviso', () => {
    render(
      <ToastProvider>
        <ToastHarness />
      </ToastProvider>,
    )
    fireEvent.click(screen.getByRole('button', { name: 'Avisar' }))
    expect(screen.getByText('Guardado')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Cerrar aviso' }))
    expect(screen.queryByText('Guardado')).toBeNull()
  })
})

describe('ConfirmDialog', () => {
  it('confirma y cancela con los textos indicados', () => {
    const onConfirm = vi.fn()
    const onCancel = vi.fn()
    render(
      <ConfirmDialog
        open
        title="¿Eliminar?"
        text="No se puede deshacer."
        confirmLabel="Eliminar"
        cancelLabel="Cancelar"
        destructive
        onConfirm={onConfirm}
        onCancel={onCancel}
      />,
    )
    expect(screen.getByRole('dialog', { name: '¿Eliminar?' })).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Eliminar' }))
    expect(onConfirm).toHaveBeenCalledTimes(1)
    fireEvent.click(screen.getByRole('button', { name: 'Cancelar' }))
    expect(onCancel).toHaveBeenCalledTimes(1)
  })

  it('con confirmPhrase exige el campo de verificación', () => {
    render(
      <ConfirmDialog
        open
        title="¿Eliminar la campaña?"
        confirmLabel="Eliminar"
        cancelLabel="Cancelar"
        confirmPhrase="Camp de prueba"
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />,
    )
    const input = screen.getByPlaceholderText('Camp de prueba')
    fireEvent.change(input, { target: { value: 'Camp de prueba' } })
    expect((input as HTMLInputElement).value).toBe('Camp de prueba')
  })
})
