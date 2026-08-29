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
 * cosas distintas.
 *
 * El atajo es la posición del botón: los del spot vienen ordenados por el
 * campo `orden` del registro, así que la tecla 1 es la primera acción DE ESE
 * SPOT — no la misma en toda la app. Un spot que no usa la acción de menor
 * orden le da otra acción a la tecla 1; lo que sí se sostiene es que el orden
 * relativo entre acciones nunca cambia de pantalla en pantalla.
 */
export function BotonesDeAccion({ acciones, deshabilitado, onElegir }: Props) {
  useEffect(() => {
    if (deshabilitado) return
    const alTeclear = (evento: KeyboardEvent) => {
      // El filtro de la tanda convive con la mesa y tiene dos campos numéricos:
      // sin esto, escribir un 1 en "Stack desde" contestaba la pregunta abierta
      // y le movía el calendario a esa casilla. Los modificadores quedan afuera
      // por lo mismo: Ctrl+1 y Alt+1 son atajos del navegador, no respuestas.
      const donde = evento.target as HTMLElement | null
      const editando = donde !== null
        && (donde.isContentEditable
          || ['input', 'textarea', 'select'].includes(donde.tagName.toLowerCase()))
      if (editando || evento.ctrlKey || evento.altKey || evento.metaKey) return

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
