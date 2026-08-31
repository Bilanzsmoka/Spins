import { useEffect, useState } from 'react'
import type {
  AccionDefinida, PreguntaDeTanda, SpotCompleto, VeredictoDeRespuesta,
} from '../../core/models/catalogo.model'
import { obtenerSpot } from '../../core/services/tablasApi'
import { FichaDeMemoria } from '../tablas/FichaDeMemoria'
import { Grilla } from '../tablas/Grilla'

/**
 * La tabla del spot, con la mano fallada resaltada.
 *
 * Se pide sólo al fallar y sólo entonces: ver la grilla es lo que convierte un
 * "no" en algo que se entiende —dónde estaba el corte, de qué bloque era esa
 * mano—, y mostrarla al acertar sería regalarle la respuesta a la siguiente.
 */
function TablaDelSpot({ pregunta, acciones }: { pregunta: PreguntaDeTanda; acciones: AccionDefinida[] }) {
  const [spot, setSpot] = useState<SpotCompleto | null>(null)

  useEffect(() => {
    let cancelado = false
    // En cero al cambiar de mano: sin esto se ve un instante la tabla del spot
    // anterior con la mano nueva resaltada, que es peor que no ver nada.
    // oxlint-disable-next-line set-state-in-effect
    setSpot(null)
    obtenerSpot(pregunta.situacion, pregunta.claveDeStack, pregunta.spot)
      .then((s) => { if (!cancelado) setSpot(s) })
      .catch(() => { if (!cancelado) setSpot(null) })
    return () => { cancelado = true }
  }, [pregunta])

  if (!spot) return null

  return (
    <div className="veredicto-tabla">
      <Grilla spot={spot} acciones={acciones} manoResaltada={pregunta.mano} />
    </div>
  )
}

interface Props {
  veredicto: VeredictoDeRespuesta
  acciones: AccionDefinida[]
  /** Cuánto tardaste. Cero cuando no se pudo medir, y entonces no se muestra. */
  milisegundos: number
  /** La casilla que se contestó, para poder mostrar su tabla al fallar. */
  pregunta: PreguntaDeTanda
  onSeguir: () => void
}

/** Segundos con un decimal: "1,8 s". Bajo el segundo, en milisegundos. */
function tiempo(ms: number) {
  return ms < 1000 ? `${ms} ms` : `${(ms / 1000).toFixed(1).replace('.', ',')} s`
}

/**
 * Qué pasó con la respuesta.
 *
 * Al acertar es una línea y seguís. Al fallar viene la ficha entera —el bloque
 * de la familia, el umbral de stack, las emparentadas, el peso en combos y el
 * tip— porque el momento en que más entra una explicación es justo el que un
 * "incorrecto" seco desaprovecha. No hay lógica nueva: es el mismo componente
 * que el popup de la grilla, en el momento en que más sirve.
 *
 * El tip no se edita desde acá: entrenando no se corrigen tablas, y abrir esa
 * puerta en medio de una tanda invita a "arreglar" la tabla en vez de aprenderla.
 */
export function Veredicto({ veredicto, acciones, milisegundos, pregunta, onSeguir }: Props) {
  const correcta = acciones.find((a) => a.clave === veredicto.accionCorrecta)

  return (
    <section className={`veredicto ${
      veredicto.acerto ? 'veredicto-bien' : veredicto.cerca ? 'veredicto-cerca' : 'veredicto-mal'
    }`}>
      <header className="veredicto-cabecera">
        {/*
          "Cerca" no es un consuelo: dice qué corregir. Erraste el tamaño, no
          el spot, y el calendario lo trata distinto por eso mismo.
        */}
        <strong>{veredicto.acerto ? 'Bien' : veredicto.cerca ? 'Cerca' : 'No'}</strong>
        <span
          className="veredicto-accion"
          style={correcta ? { background: correcta.color, color: correcta.colorTexto } : undefined}
        >
          {correcta?.etiqueta ?? veredicto.accionCorrecta}
        </span>
        {/*
          El tiempo se muestra siempre, no sólo al fallar: acertar lento es el
          error que ninguna app de tablas te señala, y es el que te cuesta en
          la mesa.
        */}
        {milisegundos > 0 && (
          <span className="veredicto-tiempo">{tiempo(milisegundos)}</span>
        )}
        {veredicto.mix && veredicto.mix.length > 1 && (
          <span className="veredicto-mix">
            mix · {veredicto.mix.map((p) => `${p.frecuencia}% ${p.accion}`).join(' / ')}
          </span>
        )}
        {/*
          Al acertar no hay botón: la mano siguiente entra sola, porque no hay
          nada que leer. El "Bien" y el tiempo se ven igual ese instante — que
          es justamente lo que hace que empieces a contestar más rápido.
        */}
        {!veredicto.acerto && (
          <button type="button" className="boton-principal" onClick={onSeguir}>
            Seguir <span className="boton-tecla">Enter</span>
          </button>
        )}
      </header>

      {!veredicto.acerto && <TablaDelSpot pregunta={pregunta} acciones={acciones} />}

      {veredicto.ficha && (
        <FichaDeMemoria
          ficha={veredicto.ficha}
          acciones={acciones}
          enLinea
          onCerrar={onSeguir}
        />
      )}
    </section>
  )
}
