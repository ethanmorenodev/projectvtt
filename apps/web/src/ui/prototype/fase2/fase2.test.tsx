import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { CampanasPrototype, InicioPrototype, PrototypeShell } from './AreasFase2'

afterEach(() => {
  cleanup()
  window.localStorage.clear()
})

describe('Prototipos Fase 2 (PMB-2.1/2.3)', () => {
  it('Inicio muestra sus bloques y solo 3 destacadas con «Ver todas»', () => {
    render(
      <PrototypeShell active="inicio">
        <InicioPrototype />
      </PrototypeShell>,
    )
    expect(screen.getByRole('heading', { name: 'Inicio' })).toBeTruthy()
    expect(screen.getByText('Continuar jugando')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Ver todas' })).toBeTruthy()
    expect(screen.getAllByRole('button', { name: 'Continuar →' })).toHaveLength(3)
    // Tiempos normalizados («hace X») y alineados como columna temporal.
    expect(screen.queryByText('ayer')).toBeNull()
    expect(screen.getAllByText('hace 1 día').length).toBeGreaterThan(0)
  })

  it('Campañas usa acciones icono accesibles, badge de estado y tiempos normalizados', () => {
    render(
      <PrototypeShell active="campanas">
        <CampanasPrototype />
      </PrototypeShell>,
    )
    expect(screen.getByRole('heading', { name: 'Campañas' })).toBeTruthy()
    expect(screen.getByText('La caída de Scaldar')).toBeTruthy()
    expect(screen.getByText('Archivada')).toBeTruthy()
    // Acción rápida: icono-flecha con etiqueta accesible por fila.
    expect(screen.getAllByRole('button', { name: /^(Continuar|Ver) / })).toHaveLength(5)
    expect(screen.getByRole('button', { name: 'Continuar La caída de Scaldar' })).toBeTruthy()
    expect(screen.queryByText('ayer')).toBeNull()
    expect(screen.getByText('hace 2 meses')).toBeTruthy()
  })

  it('los estados vacíos no duplican la CTA de la cabecera', () => {
    const first = render(
      <PrototypeShell active="campanas">
        <CampanasPrototype variant="vacio" />
      </PrototypeShell>,
    )
    expect(screen.getByText('Aún no hay campañas')).toBeTruthy()
    // Una sola «Crear campaña» (la de la cabecera).
    expect(screen.getAllByRole('button', { name: 'Crear campaña' })).toHaveLength(1)
    first.unmount()

    render(
      <PrototypeShell active="inicio">
        <InicioPrototype variant="vacio" />
      </PrototypeShell>,
    )
    // «Ver todas» desaparece con la colección vacía.
    expect(screen.queryByRole('button', { name: 'Ver todas' })).toBeNull()
    expect(screen.getAllByRole('button', { name: 'Crear campaña' })).toHaveLength(1)
  })

  it('la búsqueda sin resultados guía y ofrece limpiar', () => {
    render(
      <PrototypeShell active="campanas">
        <CampanasPrototype variant="sin-resultados" />
      </PrototypeShell>,
    )
    expect(screen.getByText('Sin resultados para «mina»')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Limpiar búsqueda' })).toBeTruthy()
  })

  it('el escenario extremo mantiene 14 filas con nombres largos y sin portada', () => {
    render(
      <PrototypeShell active="campanas">
        <CampanasPrototype variant="extremo" />
      </PrototypeShell>,
    )
    expect(screen.getAllByRole('button', { name: /^(Continuar|Ver) / })).toHaveLength(14)
    expect(screen.getByText('La épica campaña de las montañas de Scaldar y el asedio interminable de la ciudadela')).toBeTruthy()
  })
})
