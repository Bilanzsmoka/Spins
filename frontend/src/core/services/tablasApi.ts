import type {
  Catalogo, DiaDeDiario, EntradaDeDiario, EntradaEnviada,
  CategoriaDeVocabulario, FichaDeMemoria, HabitoDefinido, ParteDeMix, ProgresoDeHabitos,
  SpotCompleto, Vocabulario, GrupoDelGlosario,
} from '../models/catalogo.model'

async function pedir<T>(url: string): Promise<T> {
  const respuesta = await fetch(url)
  if (!respuesta.ok) throw new Error(`${respuesta.status} ${respuesta.statusText}`)
  return respuesta.json() as Promise<T>
}

export const obtenerCatalogo = () => pedir<Catalogo>('/api/tablas')

export const obtenerSpot = (situacion: string, stack: string, spot: string) =>
  pedir<SpotCompleto>(`/api/tablas/${situacion}/${stack}/${spot}`)

/** Le manda al servidor lo que el navegador oyó. */
export async function enviarDictado(texto: string, confianza: number): Promise<void> {
  await fetch('/api/voz/dictado', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ texto, confianza }),
  }).catch(() => {
    // Un dictado perdido no puede romper la escucha: se sigue oyendo.
  })
}

/* ---------- Diario ---------- */

export const obtenerHabitos = () => pedir<HabitoDefinido[]>('/api/diario/habitos')

export async function editarCelda(
  situacion: string, stack: string, spot: string, mano: string,
  cuerpo: { accion: string | null; mix: ParteDeMix[] | null },
): Promise<void> {
  const respuesta = await fetch(
    `/api/tablas/${situacion}/${stack}/${spot}/${mano}`,
    { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(cuerpo) },
  )
  if (!respuesta.ok) {
    const error = await respuesta.json().catch(() => null) as { error?: string } | null
    throw new Error(error?.error ?? `${respuesta.status} ${respuesta.statusText}`)
  }
}

export const obtenerFicha = (situacion: string, stack: string, spot: string, mano: string) =>
  pedir<FichaDeMemoria>(
    `/api/tablas/ficha?situacion=${encodeURIComponent(situacion)}`
    + `&stack=${encodeURIComponent(stack)}`
    + `&spot=${encodeURIComponent(spot)}`
    + `&mano=${encodeURIComponent(mano)}`)

export async function guardarTip(
  situacion: string, stack: string, spot: string, texto: string | null,
): Promise<void> {
  const respuesta = await fetch(
    `/api/tablas/${situacion}/${stack}/${spot}/tip`,
    {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ texto }),
    },
  )
  if (!respuesta.ok) {
    const error = await respuesta.json().catch(() => null) as { error?: string } | null
    throw new Error(error?.error ?? `${respuesta.status} ${respuesta.statusText}`)
  }
}

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

/* ---------- Vocabulario de voz ---------- */

export const obtenerVocabulario = () => pedir<Vocabulario>('/api/voz/vocabulario')

/** La jerga del juego. Vacía si nadie cargó el glosario todavía. */
export const obtenerGlosario = () =>
  pedir<{ grupos: GrupoDelGlosario[] }>('/api/glosario').then((r) => r.grupos)

/** Lo que salió de una captura: el texto, o por qué no hubo texto. */
export interface ResultadoDeCaptura {
  texto: string | null
  /** El codigo de error de la Web Speech API, o 'silencio' si termino sin oir nada. */
  motivo: string | null
}

/**
 * Escucha una vez y devuelve lo que el navegador oyó, sin interpretar.
 *
 * No se llama desde una pantalla: abre su propio motor sobre el mismo
 * micrófono que la escucha continua. La entrada es `capturar()` de
 * useVozDelNavegador, que pausa esa escucha mientras dura.
 */
export function capturarDictado(): Promise<ResultadoDeCaptura> {
  const Motor = (window as unknown as { SpeechRecognition?: new () => SpeechRecognition })
    .SpeechRecognition
    ?? (window as unknown as { webkitSpeechRecognition?: new () => SpeechRecognition })
      .webkitSpeechRecognition
  if (!Motor) return Promise.resolve({ texto: null, motivo: 'sin-api' })

  return new Promise((resolver) => {
    const r = new Motor()
    r.lang = 'es-ES'
    r.continuous = false
    r.interimResults = false
    r.onresult = (evento) => resolver({ texto: evento.results[0][0].transcript, motivo: null })
    // El motivo se conserva en vez de tragarse: antes todo terminaba en un
    // "no capté nada" que no distinguía un silencio de un micrófono ocupado
    // ni de un permiso denegado, y sin eso no hay forma de saber qué arreglar.
    r.onerror = (evento) => resolver({ texto: null, motivo: evento.error })
    r.onend = () => resolver({ texto: null, motivo: 'silencio' })
    try { r.start() } catch (e) { resolver({ texto: null, motivo: String(e) }) }
  })
}

async function vocabulario(url: string, metodo: string, cuerpo?: unknown): Promise<void> {
  const respuesta = await fetch(url, {
    method: metodo,
    headers: cuerpo ? { 'Content-Type': 'application/json' } : undefined,
    body: cuerpo ? JSON.stringify(cuerpo) : undefined,
  })
  if (!respuesta.ok) {
    const error = await respuesta.json().catch(() => null) as { error?: string } | null
    throw new Error(error?.error ?? `${respuesta.status} ${respuesta.statusText}`)
  }
}

export const agregarDicho = (cat: CategoriaDeVocabulario, clave: string, dicho: string) =>
  vocabulario(`/api/voz/vocabulario/${cat}/${encodeURIComponent(clave)}`, 'POST', { dicho })

export const quitarDicho = (cat: CategoriaDeVocabulario, clave: string, dicho: string) =>
  vocabulario(
    `/api/voz/vocabulario/${cat}/${encodeURIComponent(clave)}?dicho=${encodeURIComponent(dicho)}`,
    'DELETE')

/**
 * Le avisa a la voz qué tabla está abierta en pantalla. Sin esto la pantalla
 * y el copiloto llevan dos contextos separados, y al dictar una mano el
 * evento publicado arrastra la pantalla a la tabla del copiloto.
 */
export async function fijarContextoDeVoz(
  situacion: string, stackBB: number, spot: string,
): Promise<void> {
  await fetch('/api/voz/contexto', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ situacion, stackBB, spot }),
  }).catch(() => {
    // Que la voz no se entere no puede romper la pantalla: se estudia igual.
  })
}
