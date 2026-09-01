import { useEffect } from 'react'
import type { AccionDefinida } from '../../core/models/catalogo.model'

interface Props {
  acciones: AccionDefinida[]
  deshabilitado: boolean
  onElegir: (clave: string) => void
}

/**
 * Las teclas, en el orden en que están los botones: A, S y D son las tres de
 * la fila de casa, izquierda a derecha, y W queda arriba para el cuarto. Es un
 * mapa de teclado, no dato del dominio — no sale de un registro porque no
 * describe el póker, describe dónde tenés los dedos.
 *
 * Los números siguen andando como alias: ya estaban y no molestan.
 */
const TECLAS = ['a', 's', 'd', 'w', 'f', 'g', 'h', 'j', 'k']

/**
 * Los botones del spot, con el color del registro de acciones.
 *
 * El color no es decorativo: es la misma memoria visual que se entrenó
 * mirando las grillas, y pintar ALL-IN de otro color acá sería entrenar dos
 * cosas distintas.
 *
 * El atajo es la posición del botón: los del spot vienen ordenados por el
 * campo `orden` del registro, así que la tecla A es la primera acción DE ESE
 * SPOT — no la misma en toda la app. Un spot que no usa la acción de menor
 * orden le da otra acción a la tecla A; lo que sí se sostiene es que el orden
 * relativo entre acciones nunca cambia de pantalla en pantalla. Por eso cada
 * botón lleva su letra escrita: no hay nada que memorizar.
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

      const porLetra = TECLAS.indexOf(evento.key.toLowerCase())
      const indice = porLetra >= 0 ? porLetra : Number(evento.key) - 1
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
          <span className="boton-accion-tecla">
            {(TECLAS[indice] ?? String(indice + 1)).toUpperCase()}
          </span>
          {accion.etiqueta}
        </button>
      ))}
    </div>
  )
}
