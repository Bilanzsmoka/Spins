import { useEffect, useState } from 'react'
import { prepararFrase, useMejorVoz } from '../../core/voz/useMejorVoz'

/**
 * Un texto que se puede escuchar, con un botón que además lo calla.
 *
 * Vive aparte porque lo usan las dos pantallas del glosario —el diccionario y
 * los perfiles de jugador—, y porque el detalle que lo hace funcionar no es
 * obvio: `speak()` encola en vez de reemplazar, así que sin cancelar antes,
 * darle play a cinco términos seguidos los apila y los escuchás todos.
 */
export function useVozQueLee(texto: string) {
  const [hablando, setHablando] = useState(false)
  const voz = useMejorVoz()

  // Al desmontar hay que callar: si no, cambiás de página y la voz sigue
  // leyendo un término que ya no está en pantalla.
  useEffect(() => () => window.speechSynthesis?.cancel(), [])

  const decir = () => {
    if (!('speechSynthesis' in window)) return

    // Si ya está hablando, el play es un stop: es lo que espera cualquiera
    // que le da al botón de nuevo para callarlo.
    if (hablando) {
      window.speechSynthesis.cancel()
      setHablando(false)
      return
    }

    window.speechSynthesis.cancel()
    const frase = prepararFrase(texto, voz)
    frase.onend = () => setHablando(false)
    frase.onerror = () => setHablando(false)
    setHablando(true)
    window.speechSynthesis.speak(frase)
  }

  return { hablando, decir }
}
