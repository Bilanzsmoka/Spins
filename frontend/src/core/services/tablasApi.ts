import type {
  Catalogo, DiaDeDiario, EntradaDeDiario, EntradaEnviada, EstadoDeVoz,
  HabitoDefinido, ProgresoDeHabitos, SpotCompleto,
} from '../models/catalogo.model'

async function pedir<T>(url: string): Promise<T> {
  const respuesta = await fetch(url)
  if (!respuesta.ok) throw new Error(`${respuesta.status} ${respuesta.statusText}`)
  return respuesta.json() as Promise<T>
}

export const obtenerCatalogo = () => pedir<Catalogo>('/api/tablas')

export const obtenerSpot = (situacion: string, stack: string, spot: string) =>
  pedir<SpotCompleto>(`/api/tablas/${situacion}/${stack}/${spot}`)

export const obtenerEstadoDeVoz = () => pedir<EstadoDeVoz>('/api/voz/estado')

async function accionar(url: string): Promise<void> {
  const respuesta = await fetch(url, { method: 'POST' })
  if (!respuesta.ok) {
    const cuerpo = await respuesta.json().catch(() => null) as { error?: string } | null
    throw new Error(cuerpo?.error ?? `${respuesta.status} ${respuesta.statusText}`)
  }
}

export const encenderVoz = () => accionar('/api/voz/encender')
export const apagarVoz = () => accionar('/api/voz/apagar')

/* ---------- Diario ---------- */

export const obtenerHabitos = () => pedir<HabitoDefinido[]>('/api/diario/habitos')

export const obtenerProgreso = (dias = 30) =>
  pedir<ProgresoDeHabitos>(`/api/diario/progreso?dias=${dias}`)

export const obtenerDia = (fecha: string) => pedir<DiaDeDiario>(`/api/diario/${fecha}`)

export const listarDiario = (limite = 60) =>
  pedir<EntradaDeDiario[]>(`/api/diario?limite=${limite}`)

export async function guardarDia(fecha: string, entrada: EntradaEnviada): Promise<EntradaDeDiario> {
  const respuesta = await fetch(`/api/diario/${fecha}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(entrada),
  })
  if (!respuesta.ok) {
    const cuerpo = await respuesta.json().catch(() => null) as { error?: string } | null
    throw new Error(cuerpo?.error ?? `${respuesta.status} ${respuesta.statusText}`)
  }
  return respuesta.json() as Promise<EntradaDeDiario>
}
