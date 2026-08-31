import { useEffect, useState } from 'react'
import type {
  AccionDefinida, ErrorRepetido, SituacionResumen,
} from '../../core/models/catalogo.model'
import { erroresRepetidos } from '../../core/services/entrenadorApi'

interface Props {
  situaciones: SituacionResumen[]
  acciones: AccionDefinida[]
  /** Cambia al terminar una tanda, para volver a pedir la lista. */
  refrescar: number
}

/** La acción con su color, o su clave pelada si el registro no la conoce. */
function Accion({ clave, acciones }: { clave: string; acciones: AccionDefinida[] }) {
  const definida = acciones.find((a) => a.clave === clave)
  return (
    <span
      className="error-accion"
      style={definida ? { background: definida.color, color: definida.colorTexto } : undefined}
    >
      {definida?.etiqueta ?? clave}
    </span>
  )
}

/**
 * Lo que más veces erraste igual.
 *
 * No es la lista de lo que no sabés: es la de lo que sabés <b>mal</b>, que es
 * distinto y vale mucho más. Una casilla sin estudiar aparece sola en la tanda;
 * una que contestás con la misma acción equivocada cada vez es una regla
 * aprendida al revés, y ésa no se corrige repitiendo: se corrige viéndola.
 *
 * Sólo aparece lo que se repitió más de una vez. Una equivocación suelta es
 * ruido, y llenar la pantalla de ruido haría que no se mire ninguna.
 */
export function MapaDeErrores({ situaciones, acciones, refrescar }: Props) {
  const [errores, setErrores] = useState<ErrorRepetido[]>([])

  useEffect(() => {
    let cancelado = false
    erroresRepetidos()
      .then((e) => { if (!cancelado) setErrores(e) })
      .catch(() => { if (!cancelado) setErrores([]) })
    return () => { cancelado = true }
  }, [refrescar])

  if (errores.length === 0) return null

  return (
    <section className="mapa-errores">
      <h2>Lo que más te cuesta</h2>
      <p className="mapa-nota">
        Casillas que contestás mal <strong>de la misma manera</strong>. No es lo
        que no sabés: es lo que sabés al revés.
      </p>

      <ul>
        {errores.map((e) => (
          <li key={`${e.situacion}|${e.claveDeStack}|${e.spot}|${e.mano}|${e.accionElegida}`}>
            <strong className="error-mano">{e.mano}</strong>
            <span className="error-donde">
              {situaciones.find((s) => s.clave === e.situacion)?.etiqueta ?? e.situacion}
              {' · '}{e.claveDeStack}
            </span>
            <span className="error-cambio">
              <Accion clave={e.accionElegida} acciones={acciones} />
              <i>→</i>
              <Accion clave={e.accionCorrecta} acciones={acciones} />
            </span>
            <span className="error-veces">{e.veces}×</span>
          </li>
        ))}
      </ul>
    </section>
  )
}
