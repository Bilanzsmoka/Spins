import { useEffect } from 'react'
import type { PreguntaDeTanda } from '../../core/models/catalogo.model'

/** Cómo se lee cada palo en voz alta. */
const PALOS: Record<string, string> = { s: 'suited', o: 'offsuit' }

/**
 * Dice la pregunta en voz alta: primero dónde estás, después la mano.
 *
 * La mano se deletrea —"A K offsuit" y no "AKo"— porque la síntesis lee la
 * etiqueta pegada como una palabra inventada. Es la misma razón por la que
 * RedactorDeRespuesta la deletrea del lado del servidor.
 *
 * Cancela lo que se esté diciendo antes de arrancar: speak() encola en vez de
 * reemplazar, así que sin esto pasar rápido de pregunta las apila y terminás
 * escuchando la de hace tres.
 */
export function useCantarPregunta(pregunta: PreguntaDeTanda | null, activo: boolean) {
  useEffect(() => {
    if (!pregunta || !activo || !('speechSynthesis' in window)) return

    const palo = pregunta.mano.length > 2 ? PALOS[pregunta.mano[2]] ?? '' : ''
    const mano = `${pregunta.mano[0]} ${pregunta.mano[1]} ${palo}`.trim()
    const frase = new SpeechSynthesisUtterance(
      `${pregunta.etiquetaDeSpot}, ${pregunta.claveDeStack}. ${mano}.`)
    frase.lang = 'es-ES'

    window.speechSynthesis.cancel()
    window.speechSynthesis.speak(frase)

    return () => window.speechSynthesis.cancel()
  }, [pregunta, activo])
}
