import { useEffect, useState } from 'react'
import type { FraseSinEntender } from '../../core/hooks/useEventosDeVoz'
import type {
  CategoriaDeVocabulario, SituacionResumen, Vocabulario,
} from '../../core/models/catalogo.model'
import { agregarDicho, obtenerVocabulario } from '../../core/services/tablasApi'

interface Props {
  frases: FraseSinEntender[]
  situaciones: SituacionResumen[]
  situacion: string
  spot: string
  onOlvidar: (texto: string) => void
}

interface Opcion {
  clave: string
  etiqueta: string
}

/**
 * Las palabras de stack son una lista suelta, sin clave propia: el editor las
 * identifica por el nombre de su propiedad en el JSON y descarta la clave que
 * viaja en la ruta. Es la misma constante que usa la pantalla de Ajustes.
 */
const CLAVE_DE_STACK = 'palabrasDeStack'

/**
 * Todo lo que una frase suelta puede llegar a ser.
 *
 * El orden no es alfabético: arriba lo que más se dicta y más se equivoca el
 * navegador (dónde estás), abajo las piezas de una mano.
 */
const CATEGORIAS: { categoria: CategoriaDeVocabulario; titulo: string }[] = [
  { categoria: 'Formatos', titulo: 'Formato' },
  { categoria: 'Situaciones', titulo: 'Situación' },
  { categoria: 'Spots', titulo: 'Spot' },
  { categoria: 'PalabrasDeStack', titulo: 'Palabra de stack' },
  { categoria: 'Rangos', titulo: 'Rango (una carta)' },
  { categoria: 'Palos', titulo: 'Palo' },
  { categoria: 'Manos', titulo: 'Mano entera' },
  { categoria: 'Niveles', titulo: 'Palabra de nivel' },
]

/** Lo elegido para una frase mientras no se guarda. */
interface Eleccion {
  categoria: CategoriaDeVocabulario
  clave: string
  /** Solo para 'Manos': la mano se arma de partes, no se elige de una lista de 169. */
  alto: string
  bajo: string
  palo: string
}

/**
 * Las frases que el intérprete rechazó, con dónde guardarlas.
 *
 * El reconocimiento del navegador no conoce la jerga: "be be contra min
 * raise" le sale "vivir versus race", y la frase entera se descarta. La
 * salida no es adivinar más variantes —son infinitas y dependen de cómo
 * hablás vos—, sino que la app aprenda de tu voz.
 *
 * Arranca apuntando a la tabla que tenés abierta, que es la corazonada
 * correcta la mayoría de las veces: cuando te entiende mal igual corregís el
 * selector a mano para seguir, y ahí la pantalla ya sabe qué quisiste decir.
 * Pero las listas traen TODO, porque la frase mal reconocida bien puede ser
 * de otra mesa —dictaste algo de 3-max estando en heads up— y ofrecerte solo
 * lo abierto te dejaba sin forma de guardarla.
 *
 * El texto es editable porque casi nunca falla la frase entera: de "doce
 * vivir" lo que hay que enseñar es "vivir", como palabra de stack.
 *
 * Enseñar el rango es lo que más rinde —una forma nueva de "nueve" arregla
 * todas las manos con un nueve—; la mano entera existe para cuando los dos
 * rangos llegan fundidos en algo que no se puede partir.
 */
export function FrasesSinEntender({
  frases, situaciones, situacion, spot, onOlvidar,
}: Props) {
  const [vocabulario, setVocabulario] = useState<Vocabulario | null>(null)
  const [textos, setTextos] = useState<Record<string, string>>({})
  const [elecciones, setElecciones] = useState<Record<string, Eleccion>>({})
  const [guardando, setGuardando] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  // Los rangos y los palos no están en el catálogo de tablas, están en el
  // vocabulario. Se pide una vez y no cambia: lo que se enseña acá agrega
  // formas habladas, nunca claves nuevas.
  useEffect(() => {
    let cancelado = false
    obtenerVocabulario()
      .then((v) => { if (!cancelado) setVocabulario(v) })
      .catch(() => { if (!cancelado) setVocabulario(null) })
    return () => { cancelado = true }
  }, [])

  const situacionActiva = situaciones.find((s) => s.clave === situacion)

  const rangos: Opcion[] = (vocabulario?.rangos ?? [])
    .map((r) => ({ clave: r.clave, etiqueta: `${r.clave} · ${r.dichos[0] ?? ''}` }))
  const palos: Opcion[] = (vocabulario?.palos ?? [])
    .map((p) => ({ clave: p.clave, etiqueta: p.dichos[0] ?? p.clave }))
  const niveles: Opcion[] = (vocabulario?.niveles ?? [])
    .map((n) => ({ clave: n.clave, etiqueta: `${n.clave} · ${n.dichos[0] ?? ''}` }))

  const formatos: Opcion[] = [...new Set(situaciones.map((s) => s.formato))]
    .map((f) => ({ clave: f, etiqueta: f }))

  // Un mismo spot aparece en muchos stacks y muchas situaciones con la misma
  // clave, y el vocabulario tiene una sola entrada por clave: sin deduplicar,
  // la lista repetiria la misma opcion decenas de veces.
  const spots: Opcion[] = [...new Map(
    situaciones
      .flatMap((s) => s.stacks.flatMap((t) => t.spots))
      .map((p) => [p.clave, p.etiqueta] as const),
  )].map(([clave, etiqueta]) => ({ clave, etiqueta }))

  const opcionesDe = (categoria: CategoriaDeVocabulario): Opcion[] => {
    switch (categoria) {
      case 'Formatos': return formatos
      case 'Situaciones':
        return situaciones.map((s) => ({ clave: s.clave, etiqueta: s.etiqueta }))
      case 'Spots': return spots
      case 'Rangos': return rangos
      case 'Palos': return palos
      case 'Niveles': return niveles
      default: return []
    }
  }

  /** Lo que hay abierto en pantalla para esa categoría, o la primera opción. */
  const sugerida = (categoria: CategoriaDeVocabulario): string => {
    if (categoria === 'PalabrasDeStack') return CLAVE_DE_STACK
    const abierta = categoria === 'Formatos' ? situacionActiva?.formato
      : categoria === 'Situaciones' ? situacion
        : categoria === 'Spots' ? spot
          : undefined
    const opciones = opcionesDe(categoria)
    return abierta && opciones.some((o) => o.clave === abierta)
      ? abierta
      : opciones[0]?.clave ?? ''
  }

  const inicial = (categoria: CategoriaDeVocabulario): Eleccion => ({
    categoria,
    clave: sugerida(categoria),
    alto: rangos[0]?.clave ?? '',
    bajo: rangos[1]?.clave ?? '',
    palo: palos[0]?.clave ?? '',
  })

  const eleccionDe = (texto: string): Eleccion =>
    elecciones[texto] ?? inicial('Situaciones')

  const cambiar = (texto: string, parcial: Partial<Eleccion>) =>
    setElecciones((previo) => ({
      ...previo, [texto]: { ...eleccionDe(texto), ...parcial },
    }))

  const cambiarCategoria = (texto: string, categoria: CategoriaDeVocabulario) =>
    // La clave elegida era de otra categoria y ahi no significa nada: se
    // vuelve a la sugerida en vez de arrastrarla.
    setElecciones((previo) => ({ ...previo, [texto]: { ...eleccionDe(texto), ...inicial(categoria) } }))

  /**
   * La clave de la matriz para los rangos elegidos. Rango mayor primero
   * —el orden del vocabulario ya es A K Q J T 9 … 2— y sin palo si son
   * iguales, porque un par no es suited ni offsuit.
   */
  const claveDeMano = ({ alto, bajo, palo }: Eleccion): string => {
    const orden = rangos.map((r) => r.clave)
    const i = orden.indexOf(alto)
    const j = orden.indexOf(bajo)
    if (i < 0 || j < 0) return ''
    if (i === j) return alto + alto
    return i < j ? `${alto}${bajo}${palo}` : `${bajo}${alto}${palo}`
  }

  const claveAGuardar = (eleccion: Eleccion): string =>
    eleccion.categoria === 'Manos' ? claveDeMano(eleccion) : eleccion.clave

  if (frases.length === 0 || situaciones.length === 0) return null

  const ensenar = async (frase: FraseSinEntender) => {
    const dicho = (textos[frase.texto] ?? frase.texto).trim()
    const eleccion = eleccionDe(frase.texto)
    const clave = claveAGuardar(eleccion)
    if (!dicho || !clave) return

    setGuardando(frase.texto)
    setError(null)
    try {
      await agregarDicho(eleccion.categoria, clave, dicho)
      // El vocabulario del servidor es vivo: la próxima vez que lo digas ya
      // entra, sin reiniciar nada.
      onOlvidar(frase.texto)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo guardar.')
    } finally {
      setGuardando(null)
    }
  }

  return (
    <section className="sin-entender">
      <header className="sin-entender-titulo">
        <h3>Esto no lo entendí</h3>
        <p>
          Decile qué era y no vuelve a fallar. Recortá el texto si sobra algo:
          de «doce vivir» lo que hay que enseñar es «vivir». Y si te confunde
          una cosa con otra, encabezá el dictado con el nivel: «spot contra
          limp», «stack doce», «mano as rey».
        </p>
      </header>

      {error && <p className="sin-entender-error">{error}</p>}

      <ul className="sin-entender-lista">
        {frases.map((frase) => {
          const eleccion = eleccionDe(frase.texto)
          const ocupado = guardando !== null
          const esMano = eleccion.categoria === 'Manos'
          const esPar = esMano && eleccion.alto === eleccion.bajo
          return (
            <li key={frase.texto} className="sin-entender-fila">
              <span className="sin-entender-hora">{frase.hora}</span>

              <input
                className="sin-entender-texto"
                value={textos[frase.texto] ?? frase.texto}
                onChange={(e) =>
                  setTextos((previo) => ({ ...previo, [frase.texto]: e.target.value }))}
                aria-label={`Lo que se oyó: ${frase.texto}`}
              />

              <span className="sin-entender-flecha" aria-hidden="true">→</span>

              <select
                className="sin-entender-categoria"
                value={eleccion.categoria}
                disabled={ocupado}
                onChange={(e) =>
                  cambiarCategoria(frase.texto, e.target.value as CategoriaDeVocabulario)}
                aria-label="Qué clase de cosa era"
              >
                {CATEGORIAS.map((c) => (
                  <option key={c.categoria} value={c.categoria}>{c.titulo}</option>
                ))}
              </select>

              {esMano ? (
                <>
                  <select
                    className="sin-entender-rango"
                    value={eleccion.alto}
                    disabled={ocupado}
                    onChange={(e) => cambiar(frase.texto, { alto: e.target.value })}
                    aria-label="Primera carta"
                  >
                    {rangos.map((r) => (
                      <option key={r.clave} value={r.clave}>{r.etiqueta}</option>
                    ))}
                  </select>
                  <select
                    className="sin-entender-rango"
                    value={eleccion.bajo}
                    disabled={ocupado}
                    onChange={(e) => cambiar(frase.texto, { bajo: e.target.value })}
                    aria-label="Segunda carta"
                  >
                    {rangos.map((r) => (
                      <option key={r.clave} value={r.clave}>{r.etiqueta}</option>
                    ))}
                  </select>
                  <select
                    className="sin-entender-rango"
                    value={eleccion.palo}
                    // Un par no es suited ni offsuit: el palo no elige nada.
                    disabled={ocupado || esPar}
                    onChange={(e) => cambiar(frase.texto, { palo: e.target.value })}
                    aria-label="Palo"
                  >
                    {palos.map((p) => (
                      <option key={p.clave} value={p.clave}>{p.etiqueta}</option>
                    ))}
                  </select>
                  <span className="sin-entender-mano" aria-live="polite">
                    {claveDeMano(eleccion)}
                  </span>
                </>
              ) : eleccion.categoria === 'PalabrasDeStack' ? (
                // Las palabras de stack son una lista suelta: no hay cuál elegir.
                <span className="sin-entender-nota">
                  la palabra que va detrás del número
                </span>
              ) : (
                <select
                  className="sin-entender-clave"
                  value={eleccion.clave}
                  disabled={ocupado}
                  onChange={(e) => cambiar(frase.texto, { clave: e.target.value })}
                  aria-label="Cuál"
                >
                  {opcionesDe(eleccion.categoria).map((o) => (
                    <option key={o.clave} value={o.clave}>{o.etiqueta}</option>
                  ))}
                </select>
              )}

              <button
                type="button"
                className="sin-entender-guardar"
                disabled={ocupado || !claveAGuardar(eleccion)}
                onClick={() => void ensenar(frase)}
              >
                {guardando === frase.texto ? 'Guardando…' : 'Enseñar'}
              </button>

              <button
                type="button"
                className="sin-entender-descartar"
                onClick={() => onOlvidar(frase.texto)}
                title="No era una orden, descartala"
                aria-label={`Descartar «${frase.texto}»`}
              >
                ×
              </button>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
