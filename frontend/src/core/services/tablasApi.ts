import type { Catalogo, EstadoDeVoz, SpotCompleto } from '../models/catalogo.model'

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
