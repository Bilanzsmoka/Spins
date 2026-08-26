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
