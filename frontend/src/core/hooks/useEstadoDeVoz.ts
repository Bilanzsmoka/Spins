import { useCallback, useEffect, useRef, useState } from 'react'
import type { EstadoDeVoz } from '../models/catalogo.model'
import { apagarVoz, encenderVoz, obtenerEstadoDeVoz } from '../services/tablasApi'

const INTERVALO_MS = 4000

/**
 * El SSE de /api/voz/eventos solo avisa "conectado", no si el motor de
 * reconocimiento realmente arrancó. `falla` (el reconocedor no arrancó),
 * `fallaAlHablar` (la síntesis de la última respuesta falló) y `activo`
 * (el usuario lo tiene encendido) solo salen de /api/voz/estado, así que
 * se consulta por polling.
 */
export function useEstadoDeVoz() {
  const [estado, setEstado] = useState<EstadoDeVoz | null>(null)
  const [cambiando, setCambiando] = useState(false)
  const [errorAlCambiar, setErrorAlCambiar] = useState<string | null>(null)
  const vivo = useRef(true)

  const consultar = useCallback(() => {
    obtenerEstadoDeVoz()
      .then((datos) => { if (vivo.current) setEstado(datos) })
      .catch(() => { if (vivo.current) setEstado(null) })
  }, [])

  useEffect(() => {
    vivo.current = true
    consultar()
    const id = setInterval(consultar, INTERVALO_MS)
    return () => { vivo.current = false; clearInterval(id) }
  }, [consultar])

  /**
   * Enciende o apaga. Actualiza `activo` de inmediato en vez de esperar al
   * próximo sondeo, para que el botón responda al instante; si el servidor
   * rechaza, el error queda visible y el sondeo corrige el estado.
   */
  const alternar = useCallback(async () => {
    if (cambiando) return
    const encender = !(estado?.activo ?? false)
    setCambiando(true)
    setErrorAlCambiar(null)
    try {
      await (encender ? encenderVoz() : apagarVoz())
      if (vivo.current) setEstado((previo) => previo && { ...previo, activo: encender })
    } catch (e: unknown) {
      if (vivo.current) setErrorAlCambiar(e instanceof Error ? e.message : 'No se pudo cambiar la voz')
    } finally {
      if (vivo.current) setCambiando(false)
      consultar()
    }
  }, [cambiando, estado?.activo, consultar])

  return { estado, alternar, cambiando, errorAlCambiar }
}
