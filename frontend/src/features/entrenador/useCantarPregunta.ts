import { useEffect, useState } from 'react'
import type { PreguntaDeTanda, Vocabulario } from '../../core/models/catalogo.model'
import { obtenerVocabulario } from '../../core/services/tablasApi'

/**
 * Dice la pregunta en voz alta: primero dónde estás, después la mano.
 *
 * La mano se deletrea —"A K offsuit" y no "AKo"— porque la síntesis lee la
 * etiqueta pegada como una palabra inventada. Es la misma razón por la que
 * RedactorDeRespuesta la deletrea del lado del servidor. La palabra de cada
 * palo sale de `vocabulario.palos` —su forma canónica, el primer dicho—, no
 * de un literal en código: es la misma regla que ya rige acciones, spots y
 * situaciones, y un diccionario `{ s: 'suited', o: 'offsuit' }` acá sería la
 * excepción que el proyecto prohíbe. Se pide una vez, igual que hace
 * `FrasesSinEntender`, para no colgarle estado de vocabulario a la pantalla
 * que usa este hook.
 *
 * Cancela lo que se esté diciendo antes de arrancar: speak() encola en vez de
 * reemplazar, así que sin esto pasar rápido de pregunta las apila y terminás
 * escuchando la de hace tres.
 */
export function useCantarPregunta(pregunta: PreguntaDeTanda | null, activo: boolean) {
  const [vocabulario, setVocabulario] = useState<Vocabulario | null>(null)

  useEffect(() => {
    let cancelado = false
    obtenerVocabulario()
      .then((v) => { if (!cancelado) setVocabulario(v) })
      .catch(() => { if (!cancelado) setVocabulario(null) })
    return () => { cancelado = true }
  }, [])

  useEffect(() => {
    if (!pregunta || !activo || !('speechSynthesis' in window)) return

    // Sin el vocabulario todavía cargado no hay de dónde sacar la palabra:
    // se canta la mano sin el palo antes que inventarla.
    const palo = pregunta.mano.length > 2
      ? vocabulario?.palos.find((p) => p.clave === pregunta.mano[2])?.dichos[0] ?? ''
      : ''
    const mano = `${pregunta.mano[0]} ${pregunta.mano[1]} ${palo}`.trim()
    const frase = new SpeechSynthesisUtterance(
      `${pregunta.etiquetaDeSpot}, ${pregunta.claveDeStack}. ${mano}.`)
    frase.lang = 'es-ES'

    window.speechSynthesis.cancel()
    window.speechSynthesis.speak(frase)

    return () => window.speechSynthesis.cancel()
  }, [pregunta, activo, vocabulario])
}
