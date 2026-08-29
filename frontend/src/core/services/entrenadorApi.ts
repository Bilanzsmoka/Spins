import type {
  AccionDefinida, PreguntaDeTanda, RespuestaEnviada, TandaPedida, VeredictoDeRespuesta,
} from '../models/catalogo.model'

/**
 * El entrenador es lo único de la app que NO anda sin base de datos: un
 * calendario de repetición que pierde respuestas no es un calendario. Por eso
 * los errores se propagan en vez de tragarse — la pantalla los muestra.
 */
async function pedir<T>(url: string, metodo: string, cuerpo?: unknown): Promise<T> {
  const respuesta = await fetch(url, {
    method: metodo,
    headers: cuerpo ? { 'Content-Type': 'application/json' } : undefined,
    body: cuerpo ? JSON.stringify(cuerpo) : undefined,
  })
  if (!respuesta.ok) {
    const error = await respuesta.json().catch(() => null) as { error?: string } | null
    throw new Error(error?.error ?? `${respuesta.status} ${respuesta.statusText}`)
  }
  return respuesta.json() as Promise<T>
}

export const pedirTanda = (pedida: TandaPedida) =>
  pedir<PreguntaDeTanda[]>('/api/entrenador/tanda', 'POST', pedida)

export const responder = (respuesta: RespuestaEnviada) =>
  pedir<VeredictoDeRespuesta>('/api/entrenador/respuesta', 'POST', respuesta)

export const accionesDelSpot = (situacion: string, stack: string, spot: string) =>
  pedir<AccionDefinida[]>(
    `/api/entrenador/acciones?situacion=${encodeURIComponent(situacion)}`
    + `&stack=${encodeURIComponent(stack)}&spot=${encodeURIComponent(spot)}`,
    'GET')
