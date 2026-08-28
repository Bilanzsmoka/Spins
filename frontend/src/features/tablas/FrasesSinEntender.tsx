import { useState } from 'react'
import type { FraseSinEntender } from '../../core/hooks/useEventosDeVoz'
import type {
  CategoriaDeVocabulario, SituacionResumen,
} from '../../core/models/catalogo.model'
import { agregarDicho } from '../../core/services/tablasApi'

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
 * Las tres categorías a las que puede pertenecer una frase entera.
 *
 * El stack no está: es un número más una palabra ("doce be be"), no una
 * frase que mapee a una clave, así que guardar la frase entera acá estaría
 * mal. Si lo que falla es esa palabra se agrega desde Ajustes › Voz.
 */
const CATEGORIAS: { categoria: CategoriaDeVocabulario; titulo: string }[] = [
  { categoria: 'Formatos', titulo: 'Formato' },
  { categoria: 'Situaciones', titulo: 'Situación' },
  { categoria: 'Spots', titulo: 'Spot' },
]

/** Lo que el usuario eligió para una frase, mientras no la guarda. */
interface Eleccion {
  categoria: CategoriaDeVocabulario
  clave: string
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
 * Pero las listas traen TODO el catálogo, porque la frase mal reconocida
 * bien puede ser de otra mesa —dictaste algo de 3-max estando en heads up— y
 * ofrecerte solo lo abierto te dejaba sin forma de guardarla.
 *
 * El texto queda editable para cuando sobra algo: "12 vivir" cuando lo que
 * hay que enseñar es "vivir".
 */
export function FrasesSinEntender({
  frases, situaciones, situacion, spot, onOlvidar,
}: Props) {
  const [textos, setTextos] = useState<Record<string, string>>({})
  const [elecciones, setElecciones] = useState<Record<string, Eleccion>>({})
  const [guardando, setGuardando] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const situacionActiva = situaciones.find((s) => s.clave === situacion)

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

  const opcionesDe = (categoria: CategoriaDeVocabulario): Opcion[] =>
    categoria === 'Formatos' ? formatos
      : categoria === 'Situaciones'
        ? situaciones.map((s) => ({ clave: s.clave, etiqueta: s.etiqueta }))
        : spots

  /** Lo que hay abierto en pantalla para esa categoría, o la primera opción. */
  const sugerida = (categoria: CategoriaDeVocabulario): string => {
    const abierta = categoria === 'Formatos' ? situacionActiva?.formato
      : categoria === 'Situaciones' ? situacion
        : spot
    const opciones = opcionesDe(categoria)
    return abierta && opciones.some((o) => o.clave === abierta)
      ? abierta
      : opciones[0]?.clave ?? ''
  }

  const eleccionDe = (texto: string): Eleccion =>
    elecciones[texto] ?? { categoria: 'Situaciones', clave: sugerida('Situaciones') }

  if (frases.length === 0 || situaciones.length === 0) return null

  const cambiarCategoria = (texto: string, categoria: CategoriaDeVocabulario) =>
    // La clave que estaba elegida es de otra categoria y no existe en la
    // nueva: se vuelve a la sugerida en vez de arrastrar una clave que ahi no
    // significa nada.
    setElecciones((previo) => ({
      ...previo, [texto]: { categoria, clave: sugerida(categoria) },
    }))

  const cambiarClave = (texto: string, clave: string) =>
    setElecciones((previo) => ({
      ...previo, [texto]: { categoria: eleccionDe(texto).categoria, clave },
    }))

  const ensenar = async (frase: FraseSinEntender) => {
    const dicho = (textos[frase.texto] ?? frase.texto).trim()
    const { categoria, clave } = eleccionDe(frase.texto)
    if (!dicho || !clave) return

    setGuardando(frase.texto)
    setError(null)
    try {
      await agregarDicho(categoria, clave, dicho)
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
        <p>Decile qué era y no vuelve a fallar. Arranca en lo que tenés abierto.</p>
      </header>

      {error && <p className="sin-entender-error">{error}</p>}

      <ul className="sin-entender-lista">
        {frases.map((frase) => {
          const eleccion = eleccionDe(frase.texto)
          const ocupado = guardando !== null
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

              <select
                className="sin-entender-clave"
                value={eleccion.clave}
                disabled={ocupado}
                onChange={(e) => cambiarClave(frase.texto, e.target.value)}
                aria-label="Cuál"
              >
                {opcionesDe(eleccion.categoria).map((o) => (
                  <option key={o.clave} value={o.clave}>{o.etiqueta}</option>
                ))}
              </select>

              <button
                type="button"
                className="sin-entender-guardar"
                disabled={ocupado}
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
