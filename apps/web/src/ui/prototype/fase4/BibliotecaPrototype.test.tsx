import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { BibliotecaPrototype } from './BibliotecaPrototype'

/**
 * Prototipo del explorador de Biblioteca (PMB-4.1): árbol de tipos,
 * rejilla con filtros e inspector con propiedades/herencia/procedencia
 * reales del seed SRD. Variantes de revisión (vacío y extremo).
 */

afterEach(cleanup)

const grid = () => screen.getByRole('region', { name: 'Rejilla de contenido' })
const inspector = () => screen.getByRole('complementary', { name: 'Inspector' })

describe('Prototipo explorador de Biblioteca (PMB-4.1)', () => {
  it('explora árbol → rejilla → inspector con datos reales del SRD', () => {
    render(<BibliotecaPrototype />)

    // Árbol: los 3 tipos con sus conteos reales (9 razas + 9 subrazas).
    expect(screen.getByRole('button', { name: /Razas.*18/ })).toBeTruthy()
    expect(screen.getByRole('button', { name: /Clases.*12/ })).toBeTruthy()
    expect(screen.getByRole('button', { name: /Trasfondos.*13/ })).toBeTruthy()

    // Rejilla de miniaturas cuadradas con el contenido de «Razas».
    expect(within(grid()).getByRole('button', { name: /^Dwarf/ })).toBeTruthy()
    expect(screen.getByText('18 elementos')).toBeTruthy()

    // El inspector muestra el item seleccionado por defecto (Dwarf).
    expect(within(inspector()).getByRole('heading', { name: 'Dwarf' })).toBeTruthy()
    expect(within(inspector()).getByText('Velocidad: 25 ft')).toBeTruthy()
    // Procedencia real: paquete SRD con licencia CC BY 4.0.
    expect(
      within(inspector()).getByText('D&D 5e — System Reference Document 5.1'),
    ).toBeTruthy()
    expect(within(inspector()).getByRole('link', { name: 'CC BY 4.0' })).toBeTruthy()

    // Cambiar de tipo actualiza la rejilla (Clases → Fighter).
    fireEvent.click(screen.getByRole('button', { name: /Clases.*12/ }))
    expect(screen.getByText('12 elementos')).toBeTruthy()
    fireEvent.click(within(grid()).getByRole('button', { name: /Fighter/ }))
    expect(within(inspector()).getByRole('heading', { name: 'Fighter' })).toBeTruthy()
    expect(within(inspector()).getByText('Dado de vida: d10')).toBeTruthy()
  })

  it('muestra la herencia de las subrazas en el inspector', () => {
    render(<BibliotecaPrototype />)
    // Dwarf viene expandido por defecto: selecciona Hill Dwarf.
    fireEvent.click(screen.getByRole('button', { name: 'Hill Dwarf' }))
    expect(within(inspector()).getByRole('heading', { name: 'Hill Dwarf' })).toBeTruthy()
    expect(within(inspector()).getByText(/Extiende:/)).toBeTruthy()
    expect(within(inspector()).getByText('Dwarf')).toBeTruthy()
    expect(within(inspector()).getByText(/traits · languages → extend/)).toBeTruthy()
  })

  it('filtra por ámbito y ofrece limpiar filtros en el vacío', () => {
    render(<BibliotecaPrototype />)
    // El SRD no tiene contenido de campaña: 0 resultados + estado honesto.
    fireEvent.click(screen.getByRole('button', { name: 'Campaña' }))
    expect(screen.getByText('0 elementos')).toBeTruthy()
    expect(screen.getByText('No hay elementos con estos filtros.')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Limpiar filtros' }))
    expect(screen.getByText('18 elementos')).toBeTruthy()
  })

  it('variante vacío: sin selección y filtro de campaña activo', () => {
    render(<BibliotecaPrototype variant="vacio" />)
    expect(screen.getByText('0 elementos')).toBeTruthy()
    expect(
      within(inspector()).getByText(
        'Selecciona un elemento de la rejilla para ver su detalle.',
      ),
    ).toBeTruthy()
  })

  it('variante extremo: homebrew con nombre largo y lista densa sin romperse', () => {
    render(<BibliotecaPrototype variant="extremo" />)
    // El item seleccionado es el homebrew de campaña con nombre largo.
    expect(
      within(inspector()).getByRole('heading', {
        name: /Guardia de élite de la ciudadela de Scaldar/,
      }),
    ).toBeTruthy()
    expect(within(inspector()).getByText('Borrador')).toBeTruthy()
    expect(within(inspector()).getByText(/Contenido homebrew de la campaña/)).toBeTruthy()
    // Rejilla densa: 19 elementos (18 SRD + 1 homebrew).
    expect(screen.getByText('19 elementos')).toBeTruthy()
    const tiles = document.querySelectorAll('.biblio-tile')
    expect(tiles.length).toBe(19)
  })
})

describe('Prototipo Biblioteca — variante relaciones (PMB-4.6)', () => {
  it('lista relaciones tipadas entrantes/salientes y navega al destino', () => {
    render(<BibliotecaPrototype variant="relaciones" />)
    // Dwarf tiene una saliente «grants» y una entrante «leadsTo» (datos de muestra).
    expect(within(inspector()).getByText('grants')).toBeTruthy()
    expect(within(inspector()).getByText('leadsTo')).toBeTruthy()
    expect(within(inspector()).getByRole('button', { name: 'Fighter' })).toBeTruthy()

    // Navegación: el destino pasa a ser el elemento seleccionado.
    fireEvent.click(within(inspector()).getByRole('button', { name: 'Fighter' }))
    expect(within(inspector()).getByRole('heading', { name: 'Fighter' })).toBeTruthy()
    expect(screen.getByRole('status').textContent).toContain('Navegado a «Fighter»')
  })

  it('añade y quita relaciones tipadas en el editor local', () => {
    render(<BibliotecaPrototype variant="relaciones" />)
    fireEvent.click(within(inspector()).getByRole('button', { name: 'Editar relaciones' }))
    const dialog = screen.getByRole('dialog', { name: 'Editar relaciones' })
    expect(within(dialog).getByText(/Editar relaciones de «Dwarf»/)).toBeTruthy()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Añadir relación' }))
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(screen.getByRole('status').textContent).toContain('añadida a «Fighter»')
    expect(within(inspector()).getAllByText('grants')).toHaveLength(2)

    fireEvent.click(within(inspector()).getByRole('button', { name: 'Editar relaciones' }))
    const quitar = within(screen.getByRole('dialog', { name: 'Editar relaciones' }))
      .getAllByRole('button', { name: 'Quitar' })
    fireEvent.click(quitar[1])
    expect(within(inspector()).getAllByText('grants')).toHaveLength(1)
  })

  it('muestra estado vacío honesto cuando no hay relaciones', () => {
    render(<BibliotecaPrototype variant="relaciones-vacio" />)
    expect(within(inspector()).getByText(/Sin relaciones registradas/)).toBeTruthy()
  })
})

describe('Prototipo Biblioteca — variante acciones (PMB-4.5)', () => {
  it('filtra por estado y etiqueta en la rejilla', () => {
    render(<BibliotecaPrototype variant="acciones" />)
    // Razas: 18 SRD + 2 homebrew de muestra.
    expect(screen.getByText('20 elementos')).toBeTruthy()

    // Estado: solo borradores → la Veterana es la única raza en borrador.
    fireEvent.click(within(screen.getByRole('group', { name: 'Estado' })).getByRole('button', { name: 'Borrador' }))
    expect(screen.getByText('1 elementos')).toBeTruthy()
    expect(within(grid()).getByRole('button', { name: /Veterana de la guardia/ })).toBeTruthy()

    // Etiqueta: «guardia» deja las 2 razas homebrew; «piratas» no hay en Razas.
    fireEvent.click(within(screen.getByRole('group', { name: 'Estado' })).getByRole('button', { name: 'Todos' }))
    fireEvent.click(within(screen.getByRole('group', { name: 'Etiqueta' })).getByRole('button', { name: 'guardia' }))
    expect(screen.getByText('2 elementos')).toBeTruthy()
    fireEvent.click(within(screen.getByRole('group', { name: 'Etiqueta' })).getByRole('button', { name: 'piratas' }))
    expect(screen.getByText('0 elementos')).toBeTruthy()
    expect(screen.getByText('No hay elementos con estos filtros.')).toBeTruthy()

    // Limpiar filtros restaura la lista completa.
    fireEvent.click(screen.getByRole('button', { name: 'Limpiar filtros' }))
    expect(screen.getByText('20 elementos')).toBeTruthy()
  })

  it('ordena por «Uso» con los conteos de muestra', () => {
    render(<BibliotecaPrototype variant="acciones" />)
    fireEvent.change(screen.getByRole('combobox', { name: 'Orden' }), {
      target: { value: 'uso' },
    })
    const names = [...document.querySelectorAll('.biblio-tile__name')].map(
      (node) => node.textContent ?? '',
    )
    // 4 usos → 1 uso → el resto a 0 ordenado por nombre.
    expect(names[0]).toBe('Guardia de Scaldar')
    expect(names[1]).toBe('Veterana de la guardia')
    expect(names[2]).toBe('Dragonborn')
  })

  it('duplicar pide confirmación y avisa de la copia personal', () => {
    render(<BibliotecaPrototype variant="acciones" />)
    fireEvent.click(within(inspector()).getByRole('button', { name: 'Duplicar' }))
    const dialog = screen.getByRole('dialog', { name: 'Duplicar «Guardia de Scaldar»' })
    expect(within(dialog).getByText(/biblioteca personal/)).toBeTruthy()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Duplicar' }))
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(
      screen.getByRole('status').textContent,
    ).toContain('Copia creada en tu biblioteca personal: «Guardia de Scaldar (copia)».')
  })

  it('ver usos lista las filas del contrato y ofrece estado vacío', () => {
    render(<BibliotecaPrototype variant="acciones" />)
    // Guardia de Scaldar: 4 usos de muestra (sin snapshots).
    fireEvent.click(within(inspector()).getByRole('button', { name: 'Ver usos' }))
    const panel = screen.getByRole('region', { name: 'Usos del elemento' })
    expect(within(panel).getAllByRole('listitem')).toHaveLength(4)
    expect(within(panel).getByText('Kaelen Piedra Alta')).toBeTruthy()
    // Dos filas comparten «La Corona Rota · rev. 3» (Kaelen y Thalor).
    expect(within(panel).getAllByText(/La Corona Rota · rev\. 3/)).toHaveLength(2)
    expect(within(panel).getByText(/los snapshots no se muestran/)).toBeTruthy()

    // Pirata de la Escarlata no tiene usos → el panel sigue abierto y muestra
    // el estado vacío honesto.
    fireEvent.click(screen.getByRole('button', { name: /Trasfondos.*14/ }))
    fireEvent.click(within(grid()).getByRole('button', { name: /Pirata de la Escarlata/ }))
    expect(screen.getByText(/Sin usos registrados/)).toBeTruthy()
  })

  it('Publicar queda deshabilitado y Mover avanza hacia el editor', () => {
    render(<BibliotecaPrototype variant="acciones" />)
    const publish = within(inspector()).getByRole('button', { name: 'Publicar' })
    expect((publish as HTMLButtonElement).disabled).toBe(true)
    expect(within(inspector()).getByText(/Publicar vive en el editor/)).toBeTruthy()

    fireEvent.click(within(inspector()).getByRole('button', { name: 'Mover' }))
    expect(
      screen.getByRole('status').textContent,
    ).toContain('Mover cambia la jerarquía desde el editor (sección Organización).')
  })
})
