import { useEffect, useState } from 'react'
import type { EventoDeVoz } from '../models/catalogo.model'

/**
 * Se suscribe al canal SSE del copiloto. EventSource reconecta solo,
 * asi que no hace falta watchdog del lado del navegador.
 */
export function useEventosDeVoz() {
  const [ultimo, setUltimo] = useState<EventoDeVoz | null>(null)
  const [conectado, setConectado] = useState(false)

  useEffect(() => {
    const fuente = new EventSource('/api/voz/eventos')
    fuente.onopen = () => setConectado(true)
    fuente.onerror = () => setConectado(false)
    fuente.onmessage = (mensaje) => setUltimo(JSON.parse(mensaje.data) as EventoDeVoz)
    return () => fuente.close()
  }, [])

  return { ultimo, conectado }
}
