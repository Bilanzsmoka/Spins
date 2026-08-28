import { useCallback, useEffect, useRef, useState } from 'react'
import { enviarDictado } from '../services/tablasApi'

/** La API vive con prefijo en Chrome y sin prefijo en el estándar. */
type ConstructorDeReconocimiento = new () => SpeechRecognition
const Reconocimiento: ConstructorDeReconocimiento | undefined =
  (window as unknown as { SpeechRecognition?: ConstructorDeReconocimiento }).SpeechRecognition
  ?? (window as unknown as { webkitSpeechRecognition?: ConstructorDeReconocimiento }).webkitSpeechRecognition

/**
 * El copiloto del lado del navegador: oye con la Web Speech API y habla con
 * speechSynthesis.
 *
 * Los dos van juntos por una razón concreta: mientras la app habla hay que
 * dejar de escuchar, o el micrófono toma la respuesta y dispara una consulta
 * con la propia voz de la app. Teniendo las dos puntas acá, silenciar es
 * apagar el reconocimiento durante la frase.
 */
export function useVozDelNavegador(respuesta: string | null) {
  const [activo, setActivo] = useState(false)
  const [falla, setFalla] = useState<string | null>(null)
  const motor = useRef<SpeechRecognition | null>(null)
  const hablando = useRef(false)

  const disponible = Reconocimiento !== undefined

  useEffect(() => {
    if (!disponible || !activo) return

    const r = new Reconocimiento!()
    r.lang = 'es-ES'
    r.continuous = true
    // Los parciales son una frase a medio formar: resolverlos daría
    // respuestas contra manos que todavía no se terminaron de decir.
    r.interimResults = false

    r.onresult = (evento) => {
      if (hablando.current) return
      const ultimo = evento.results[evento.results.length - 1]
      if (!ultimo.isFinal) return
      void enviarDictado(ultimo[0].transcript, ultimo[0].confidence || 0.9)
    }
    r.onerror = (evento) => {
      // "no-speech" es silencio, no una falla: Chrome lo emite todo el tiempo.
      if (evento.error !== 'no-speech') setFalla(evento.error)
    }
    // Chrome corta la escucha continua sola cada tanto; reengancharla acá es
    // el equivalente del watchdog que tenía el reconocedor de SAPI.
    r.onend = () => { if (activo && !hablando.current) try { r.start() } catch { /* ya corriendo */ } }

    motor.current = r
    try { r.start() } catch (e) { setFalla(String(e)) }

    return () => { motor.current = null; r.onend = null; r.stop() }
  }, [disponible, activo])

  // Hablar la respuesta, con el micrófono apagado mientras dura.
  useEffect(() => {
    if (!respuesta || !activo) return
    hablando.current = true
    motor.current?.stop()

    const frase = new SpeechSynthesisUtterance(respuesta)
    frase.lang = 'es-ES'
    frase.onend = () => {
      hablando.current = false
      try { motor.current?.start() } catch { /* ya corriendo */ }
    }
    window.speechSynthesis.speak(frase)
  }, [respuesta, activo])

  const alternar = useCallback(() => {
    setFalla(null)
    setActivo((previo) => !previo)
  }, [])

  return { disponible, activo, falla, alternar }
}
