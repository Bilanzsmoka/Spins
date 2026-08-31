import type {
  AccionDefinida, ErrorRepetido, PreguntaDeTanda, RespuestaEnviada, TandaPedida,
  VeredictoDeRespuesta,
} from '../models/catalogo.model'

/**
 * Un fallo del servidor con su código HTTP a mano.
 *
 * El código viaja porque la pantalla trata dos fallos distinto: un 404 de
 * `/respuesta` es la casilla que dejó de existir —el controlador lo documenta
 * y la pantalla saltea la pregunta— y cualquier otro es un error que se
 * muestra. Se distingue por código y no por el texto del mensaje: reescribir
 * esa frase en el controlador no puede romper el salteo en silencio.
 */
export class ErrorDeApi extends Error {
  readonly estado: number

  constructor(mensaje: string, estado: number) {
    super(mensaje)
    this.name = 'ErrorDeApi'
    this.estado = estado
  }
}

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
    throw new ErrorDeApi(
      error?.error ?? `${respuesta.status} ${respuesta.statusText}`, respuesta.status)
  }
  return respuesta.json() as Promise<T>
}

export const pedirTanda = (pedida: TandaPedida) =>
  pedir<PreguntaDeTanda[]>('/api/entrenador/tanda', 'POST', pedida)

export const responder = (respuesta: RespuestaEnviada) =>
  pedir<VeredictoDeRespuesta>('/api/entrenador/respuesta', 'POST', respuesta)

/** Lo que más veces erraste igual. Vacío mientras no se repita nada. */
export const erroresRepetidos = () =>
  pedir<ErrorRepetido[]>('/api/entrenador/errores', 'GET')

export const accionesDelSpot = (situacion: string, stack: string, spot: string) =>
  pedir<AccionDefinida[]>(
    `/api/entrenador/acciones?situacion=${encodeURIComponent(situacion)}`
    + `&stack=${encodeURIComponent(stack)}&spot=${encodeURIComponent(spot)}`,
    'GET')

/**
 * Contestar hablando. El servidor devuelve `{ ignorado: true }` cuando el
 * texto no era una acción: hablar cerca del micrófono no puede contar como
 * fallo, así que eso llega como null y la pregunta sigue abierta.
 */
export async function responderHablado(
  situacion: string, claveDeStack: string, spot: string, mano: string, texto: string,
  milisegundos: number,
): Promise<VeredictoDeRespuesta | null> {
  const v = await pedir<VeredictoDeRespuesta | { ignorado: true }>(
    '/api/entrenador/respuesta-hablada', 'POST',
    { situacion, claveDeStack, spot, mano, texto, milisegundos })

  return 'ignorado' in v ? null : v
}
