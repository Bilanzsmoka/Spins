import type { AccionDefinida, VeredictoDeRespuesta } from '../../core/models/catalogo.model'
import { FichaDeMemoria } from '../tablas/FichaDeMemoria'

interface Props {
  veredicto: VeredictoDeRespuesta
  acciones: AccionDefinida[]
  onSeguir: () => void
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
export function Veredicto({ veredicto, acciones, onSeguir }: Props) {
  const correcta = acciones.find((a) => a.clave === veredicto.accionCorrecta)

  return (
    <section className={`veredicto ${veredicto.acerto ? 'veredicto-bien' : 'veredicto-mal'}`}>
      <header className="veredicto-cabecera">
        <strong>{veredicto.acerto ? 'Bien' : 'No'}</strong>
        <span
          className="veredicto-accion"
          style={correcta ? { background: correcta.color, color: correcta.colorTexto } : undefined}
        >
          {correcta?.etiqueta ?? veredicto.accionCorrecta}
        </span>
        {veredicto.mix && veredicto.mix.length > 1 && (
          <span className="veredicto-mix">
            mix · {veredicto.mix.map((p) => `${p.frecuencia}% ${p.accion}`).join(' / ')}
          </span>
        )}
        <button type="button" className="boton-principal" onClick={onSeguir}>
          Seguir
        </button>
      </header>

      {veredicto.ficha && (
        <FichaDeMemoria
          ficha={veredicto.ficha}
          acciones={acciones}
          onCerrar={onSeguir}
        />
      )}
    </section>
  )
}
