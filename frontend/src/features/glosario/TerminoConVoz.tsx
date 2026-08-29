import { useEffect, useState } from 'react'

interface Props {
  termino: string
  explicacion: string
}

/**
 * Un término con su explicación y un play que lee las dos cosas.
 *
 * Lee el renglón entero y no sólo la palabra: la idea es poder estudiar sin
 * mirar la pantalla, igual que el resto de la app. Oír "limp" suelto no
 * enseña nada; oír "limp: entrar pagando exactamente la ciega grande, sin
 * subir" sí.
 */
export function TerminoConVoz({ termino, explicacion }: Props) {
  const [hablando, setHablando] = useState(false)

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

    // speak() encola en vez de reemplazar, así que sin cancelar antes, darle
    // play a cinco términos seguidos los apila y los escuchás todos.
    window.speechSynthesis.cancel()
    const frase = new SpeechSynthesisUtterance(`${termino}. ${explicacion}`)
    frase.lang = 'es-ES'
    frase.onend = () => setHablando(false)
    frase.onerror = () => setHablando(false)
    setHablando(true)
    window.speechSynthesis.speak(frase)
  }

  return (
    <li className="glosario-termino">
      <button
        type="button"
        className={`boton-play${hablando ? ' boton-play-activo' : ''}`}
        onClick={decir}
        aria-label={hablando ? `Callar ${termino}` : `Escuchar ${termino}`}
      >
        {hablando ? '■' : '▶'}
      </button>
      <div>
        <strong className="glosario-palabra">{termino}</strong>
        <p className="glosario-explicacion">{explicacion}</p>
      </div>
    </li>
  )
}
