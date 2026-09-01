import { useEffect } from 'react'
import type { PreguntaDeTanda } from '../../core/models/catalogo.model'

/**
 * Dice dónde estás parado, y nada más.
 *
 * **No canta la mano a propósito.** Decirla convertía el ejercicio en escuchar
 * un código en vez de leer dos cartas, que es lo que vas a tener que hacer en
 * la mesa. La mano sólo se dice al fallar, cuando ya no hay nada que resolver
 * y lo que queda es fijarla.
 *
 * Cancela lo que se esté diciendo antes de arrancar: speak() encola en vez de
 * reemplazar, así que sin esto pasar rápido de pregunta las apila y terminás
 * escuchando la de hace tres.
 */
export function useCantarPregunta(pregunta: PreguntaDeTanda | null, activo: boolean) {
  useEffect(() => {
    if (!pregunta || !activo || !('speechSynthesis' in window)) return

    const frase = new SpeechSynthesisUtterance(
      `${pregunta.etiquetaDeSpot}, ${pregunta.claveDeStack}.`)
    frase.lang = 'es-ES'

    window.speechSynthesis.cancel()
    window.speechSynthesis.speak(frase)

    return () => window.speechSynthesis.cancel()
  }, [pregunta, activo])
}
