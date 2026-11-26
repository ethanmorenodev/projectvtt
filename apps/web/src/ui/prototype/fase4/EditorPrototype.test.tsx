import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { EditorPrototype } from './EditorPrototype'

/**
 * Prototipo del editor universal (PMB-4.4): formulario por secciones que se
 * adapta al TypeSchema real de `race`, herencia por campo con sus modos,
 * previsualización en vivo, errores con los mensajes del servidor y los
 * modos de creación y solo lectura.
 */

afterEach(cleanup)

const form = () => screen.getByRole('region', { name: 'Formulario de contenido' })
const statePanel = () => screen.getByRole('complementary', { name: 'Estado y versionado' })

describe('Prototipo del editor universal (PMB-4.4)', () => {
  it('organiza el formulario en las secciones de LIBRARY §3 con el esquema real de `race`', () => {
    render(<EditorPrototype />)

    for (const section of [
      'Identidad',
      'Organización',
      'Propiedades',
      'Capacidades',
      'Requisitos y restricciones',
      'Opciones elegibles',
      'Visibilidad y permisos',
    ]) {
      expect(screen.getByText(section)).toBeTruthy()
    }

    // Los 5 campos tipados del TypeSchema de `race` (seed dnd5e-srd-5.1).
    for (const label of [
      'Bonificaciones de atributo',
      'Velocidad',
      'Tamaño',
      'Idiomas',
      'Rasgos',
    ]) {
      expect(screen.getByText(label)).toBeTruthy()
    }

    // Secciones fuera del alcance del hito: honestas y desactivadas.
    expect(form().textContent).toContain('Fase 5 (PMB-5.2)')
    expect(form().textContent).toContain('Fase 5 (PMB-5.3)')
  })

  it('hereda del padre por defecto y permite cambiar el modo por campo', () => {
    render(<EditorPrototype />)

    // speed y size no tienen modo propio → heredan de Dwarf (modo por defecto).
    expect(screen.getAllByText(/Heredado de Dwarf:/).length).toBe(2)

    // Al pasar speed a «Sobrescribir» aparece el control editable y el
    // ayudante explica qué sustituye del padre.
    const speedModes = screen.getByRole('group', { name: 'Herencia de Velocidad' })
    fireEvent.click(within(speedModes).getByRole('button', { name: 'Sobrescribir' }))

    const speedInput = screen.getByLabelText('Velocidad') as HTMLInputElement
    expect(speedInput.tagName).toBe('INPUT')
    expect(speedInput.disabled).toBe(false)
    expect(screen.getByText(/Sustituye el valor del padre \(25\)/)).toBeTruthy()
  })

  it('mantiene la previsualización viva mientras se escribe', () => {
    render(<EditorPrototype />)

    expect(statePanel().textContent).toContain('Hill Dwarf de Scaldar')

    const nameInput = screen.getByLabelText('Nombre') as HTMLInputElement
    fireEvent.change(nameInput, { target: { value: 'Guardia de Scaldar' } })

    expect(statePanel().textContent).toContain('Guardia de Scaldar')
    expect(screen.getByText('Cambios sin guardar')).toBeTruthy()
  })

  it('variante error: banner, mensajes reales de ContentSchema y foco en el primer campo', () => {
    render(<EditorPrototype variant="error" />)

    expect(screen.getByRole('alert').textContent).toContain('No se pudo guardar el borrador.')

    // Las claves ausentes se marcan con el mensaje exacto del servidor.
    expect(screen.getByText("Falta la propiedad obligatoria 'speed'.")).toBeTruthy()
    expect(screen.getByText("Falta la propiedad obligatoria 'size'.")).toBeTruthy()

    const speedInput = screen.getByLabelText('Velocidad') as HTMLInputElement
    expect(speedInput.getAttribute('aria-invalid')).toBe('true')
    expect(document.activeElement).toBe(speedInput)
  })

  it('variante nuevo: creación con ámbito personal y procedencia vacía', () => {
    render(<EditorPrototype variant="nuevo" />)

    expect(screen.getByRole('heading', { name: 'Nuevo contenido' })).toBeTruthy()

    const scope = screen.getByLabelText('Ámbito') as HTMLSelectElement
    expect(scope.tagName).toBe('SELECT')
    expect(scope.value).toBe('user')

    expect(screen.getByRole('button', { name: 'Crear borrador' })).toBeTruthy()
    expect(statePanel().textContent).toContain('Se creará en tu biblioteca personal')
    expect(statePanel().textContent).toContain('Sin revisiones publicadas todavía.')
  })

  it('variante solo-lectura: SRD oficial con controles desactivados', () => {
    render(<EditorPrototype variant="solo-lectura" />)

    expect(screen.getByText('Contenido oficial del SRD — solo lectura.')).toBeTruthy()
    expect(screen.getByText('Solo lectura')).toBeTruthy()

    const nameInput = screen.getByLabelText('Nombre') as HTMLInputElement
    expect(nameInput.disabled).toBe(true)
    // speed/size siguen heredados (output); comprobamos un control editable.
    expect((screen.getByLabelText('rasgo 1') as HTMLInputElement).disabled).toBe(true)

    // Sin acciones de escritura: ni pie de formulario ni publicación propia.
    expect(screen.queryByRole('button', { name: 'Guardar borrador' })).toBeNull()
    expect((screen.getByRole('button', { name: 'Publicar' }) as HTMLButtonElement).disabled).toBe(
      true,
    )
  })
})
