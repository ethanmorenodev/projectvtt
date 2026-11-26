/** Datos de MUESTRA del prototipo del flujo del DM (PMB-4.8). */

export type Workflow = 'borrador' | 'propuesta' | 'publicado'
export type Permission = 'view' | 'edit'

export interface ProtoProposal {
  id: string
  name: string
  author: string
  state: Workflow
  /** Resumen legible del diff frente a la última revisión publicada. */
  changes: string[]
  revision: number
}

export interface ProtoGrant {
  id: string
  subject: string
  kind: 'user' | 'role'
  permission: Permission
}

export const SAMPLE_PROPOSALS: ProtoProposal[] = [
  {
    id: 'p-1',
    name: 'Guardia de Scaldar',
    author: 'Mira Colmillo',
    state: 'propuesta',
    changes: ['velocidad: 25 → 30', '+ rasgo «Veterano»', 'descripción revisada'],
    revision: 1,
  },
  {
    id: 'p-2',
    name: 'Ritual de la corona',
    author: 'Kaelen Piedra Alta',
    state: 'propuesta',
    changes: ['+ requisito: nivel 3', '+ coste: 50 pg'],
    revision: 2,
  },
  {
    id: 'p-3',
    name: 'Ama de llaves',
    author: 'Mira Colmillo',
    state: 'borrador',
    changes: ['contenido nuevo (sin revisión publicada)'],
    revision: 0,
  },
]

export const SAMPLE_GRANTS: ProtoGrant[] = [
  { id: 'g-1', subject: 'Kaelen Piedra Alta', kind: 'user', permission: 'edit' },
  { id: 'g-2', subject: 'GM', kind: 'role', permission: 'view' },
]
