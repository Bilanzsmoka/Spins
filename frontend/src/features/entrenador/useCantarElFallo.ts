import { useEffect } from 'react'
import type { AccionDefinida, VeredictoDeRespuesta } from '../../core/models/catalogo.model'

/**
 * Dice en voz alta qué había que hacer, y por qué, cuando fallás.
 *
 * Antes la voz cantaba la pregunta y se callaba en el momento que más importa:
 * el error. Estudiando sin mirar la pantalla, fallar sonaba igual que acertar.
 *
 * No dice sólo la acción: dice la **regla del grupo** cuando la hay —"los Ax
 * offsuit: ALL-IN hasta A5o"—, que es lo que generaliza. Acordarse de una
 * regla arregla las trece manos de esa fila; acordarse de una casilla arregla
 * una.
 */
export function useCantarElFallo(
  veredicto: VeredictoDeRespuesta | null,
  acciones: AccionDefinida[],
  activo: boolean,
) {
  useEffect(() => {
    if (!veredicto || veredicto.acerto || !activo) return
    if (!('speechSynthesis' in window)) return

    const etiqueta = (clave: string) =>
      acciones.find((a) => a.clave === clave)?.etiqueta ?? clave

    const partes = [veredicto.cerca ? 'Cerca.' : 'No.', `Era ${etiqueta(veredicto.accionCorrecta)}.`]

    // La primera regla es la que más manos cubre: es la que conviene repetir.
    const regla = veredicto.ficha?.reglas?.[0]
    if (regla)
      partes.push(regla.hasta
        ? `${regla.grupo}: ${etiqueta(regla.accion)} hasta ${regla.hasta}.`
        : `${regla.grupo}: todos ${etiqueta(regla.accion)}.`)

    const frase = new SpeechSynthesisUtterance(partes.join(' '))
    frase.lang = 'es-ES'

    // Cancelar antes: si no, esto se encola detrás de la pregunta que se
    // estaba cantando y llega cuando ya pasaste a la siguiente mano.
    window.speechSynthesis.cancel()
    window.speechSynthesis.speak(frase)

    return () => window.speechSynthesis.cancel()
  }, [veredicto, acciones, activo])
}
