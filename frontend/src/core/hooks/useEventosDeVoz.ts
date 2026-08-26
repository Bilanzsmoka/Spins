import { useEffect, useState } from 'react'
import type { ConsultaRegistrada, EventoDeVoz } from '../models/catalogo.model'

/** Cuántas consultas se conservan escritas en pantalla. */
const MAXIMO_HISTORIAL = 30

/**
 * Se suscribe al canal SSE del copiloto. EventSource reconecta solo,
 * asi que no hace falta watchdog del lado del navegador.
 *
 * Ademas del ultimo evento guarda un historial acotado: la respuesta se
 * habla, pero hablada se pierde. Escrita queda, y sirve para repasar la
 * tanda al terminar de jugar.
 */
export function useEventosDeVoz() {
  const [ultimo, setUltimo] = useState<EventoDeVoz | null>(null)
  const [historial, setHistorial] = useState<ConsultaRegistrada[]>([])
  const [conectado, setConectado] = useState(false)

  useEffect(() => {
    const fuente = new EventSource('/api/voz/eventos')
    fuente.onopen = () => setConectado(true)
    fuente.onerror = () => setConectado(false)
    fuente.onmessage = (mensaje) => {
      const evento = JSON.parse(mensaje.data) as EventoDeVoz
      setUltimo(evento)
      const hora = new Date().toLocaleTimeString('es', {
        hour: '2-digit', minute: '2-digit', second: '2-digit',
      })
      setHistorial((previo) => [{ ...evento, hora }, ...previo].slice(0, MAXIMO_HISTORIAL))
    }
    return () => fuente.close()
  }, [])

  const limpiarHistorial = () => setHistorial([])

  return { ultimo, historial, conectado, limpiarHistorial }
}
