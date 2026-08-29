import { useEffect } from 'react'
import type { AccionDefinida } from '../../core/models/catalogo.model'

interface Props {
  acciones: AccionDefinida[]
  deshabilitado: boolean
  onElegir: (clave: string) => void
}

/**
 * Los botones del spot, con el color del registro de acciones.
 *
 * El color no es decorativo: es la misma memoria visual que se entrenó
 * mirando las grillas, y pintar ALL-IN de otro color acá sería entrenar dos
 * cosas distintas. El atajo de teclado sale del campo `orden` del registro,
 * así que la tecla 1 es siempre la misma acción en toda la app.
 */
export function BotonesDeAccion({ acciones, deshabilitado, onElegir }: Props) {
  useEffect(() => {
    if (deshabilitado) return
    const alTeclear = (evento: KeyboardEvent) => {
      const indice = Number(evento.key) - 1
      const accion = acciones[indice]
      if (!Number.isNaN(indice) && accion) onElegir(accion.clave)
    }
    window.addEventListener('keydown', alTeclear)
    return () => window.removeEventListener('keydown', alTeclear)
  }, [acciones, deshabilitado, onElegir])

  return (
    <div className="botones-accion">
      {acciones.map((accion, indice) => (
        <button
          key={accion.clave}
          type="button"
          className="boton-accion"
          disabled={deshabilitado}
          style={{ background: accion.color, color: accion.colorTexto }}
          onClick={() => onElegir(accion.clave)}
        >
          <span className="boton-accion-tecla">{indice + 1}</span>
          {accion.etiqueta}
        </button>
      ))}
    </div>
  )
}
