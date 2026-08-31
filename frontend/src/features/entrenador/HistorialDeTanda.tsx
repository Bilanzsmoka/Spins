import type { AccionDefinida } from '../../core/models/catalogo.model'

/** Una mano ya contestada, tal como quedó. */
export interface ManoContestada {
  mano: string
  elegida: string
  correcta: string
  acerto: boolean
  cerca: boolean
  milisegundos: number
  etiquetaDeSpot: string
}

interface Props {
  manos: ManoContestada[]
  acciones: AccionDefinida[]
}

const tiempo = (ms: number) =>
  ms < 1000 ? `${ms} ms` : `${(ms / 1000).toFixed(1).replace('.', ',')} s`

function Accion({ clave, acciones }: { clave: string; acciones: AccionDefinida[] }) {
  const definida = acciones.find((a) => a.clave === clave)
  return (
    <span
      className="historial-accion"
      style={definida ? { background: definida.color, color: definida.colorTexto } : undefined}
    >
      {definida?.etiqueta ?? clave}
    </span>
  )
}

/**
 * Lo que llevás contestado en esta tanda, a mano y sin salir de la pantalla.
 *
 * Existe porque al acertar la mesa pasa sola a la siguiente: sin esto, la mano
 * que acabás de resolver desaparece y no queda dónde mirarla. Acá quedan todas,
 * con lo que contestaste, lo que decía la tabla y cuánto tardaste — que es
 * material de estudio, no un marcador.
 *
 * Lo más reciente arriba: es lo que se quiere volver a mirar.
 */
export function HistorialDeTanda({ manos, acciones }: Props) {
  if (manos.length === 0) return null

  return (
    <section className="historial">
      <h2>Lo que va de la tanda</h2>
      <ul>
        {manos.map((m, i) => (
          <li
            key={`${m.mano}-${manos.length - i}`}
            className={m.acerto ? 'historial-bien' : m.cerca ? 'historial-cerca' : 'historial-mal'}
          >
            <strong className="historial-mano">{m.mano}</strong>
            <span className="historial-spot">{m.etiquetaDeSpot}</span>
            {m.acerto ? (
              <Accion clave={m.correcta} acciones={acciones} />
            ) : (
              <span className="historial-cambio">
                <Accion clave={m.elegida} acciones={acciones} />
                <i>→</i>
                <Accion clave={m.correcta} acciones={acciones} />
              </span>
            )}
            {m.milisegundos > 0 && (
              <span className="historial-tiempo">{tiempo(m.milisegundos)}</span>
            )}
          </li>
        ))}
      </ul>
    </section>
  )
}
