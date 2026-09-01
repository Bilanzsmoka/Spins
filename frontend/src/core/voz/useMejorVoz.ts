import { useEffect, useState } from 'react'

/**
 * De mejor a peor, por lo que trae el nombre de la voz.
 *
 * Los navegadores no dicen qué voz suena bien: hay que reconocerlas por el
 * nombre. Las "Natural" y "Online" de Edge son las neuronales —las que suenan
 * a persona—; las de Google vienen después; y al final quedan las locales del
 * sistema, que son las robóticas de siempre.
 */
const PREFERIDAS = ['natural', 'online', 'google', 'premium', 'enhanced']

const puntaje = (voz: SpeechSynthesisVoice) => {
  const nombre = voz.name.toLowerCase()
  const i = PREFERIDAS.findIndex((p) => nombre.includes(p))
  return i < 0 ? PREFERIDAS.length : i
}

/**
 * La mejor voz en castellano que tenga este navegador.
 *
 * Sin esto la síntesis usa la voz por defecto del sistema, que en Windows es
 * la robótica de toda la vida. Chrome y Edge traen voces neuronales instaladas
 * y sólo hay que pedirlas.
 *
 * `getVoices()` suele devolver vacío en la primera llamada porque la lista se
 * carga aparte: por eso se escucha `voiceschanged`, que es el aviso de que ya
 * están. Sin ese enganche, la primera frase de la sesión sale con la voz fea y
 * las demás bien.
 */
export function useMejorVoz(idioma = 'es') {
  const [voz, setVoz] = useState<SpeechSynthesisVoice | null>(null)

  useEffect(() => {
    if (!('speechSynthesis' in window)) return

    const elegir = () => {
      const enIdioma = window.speechSynthesis.getVoices()
        .filter((v) => v.lang.toLowerCase().startsWith(idioma))
      if (enIdioma.length === 0) return
      setVoz([...enIdioma].sort((a, b) => puntaje(a) - puntaje(b))[0])
    }

    elegir()
    window.speechSynthesis.addEventListener('voiceschanged', elegir)
    return () => window.speechSynthesis.removeEventListener('voiceschanged', elegir)
  }, [idioma])

  return voz
}

/**
 * Deja la frase lista para hablar con la mejor voz disponible.
 *
 * Un poco más lenta que el valor por defecto: la corrección de una mano son
 * tres datos seguidos —qué era, de qué grupo, hasta dónde— y a velocidad
 * normal se pisan entre sí.
 */
export function prepararFrase(texto: string, voz: SpeechSynthesisVoice | null) {
  const frase = new SpeechSynthesisUtterance(texto)
  frase.lang = voz?.lang ?? 'es-ES'
  if (voz) frase.voice = voz
  frase.rate = 0.95
  return frase
}
