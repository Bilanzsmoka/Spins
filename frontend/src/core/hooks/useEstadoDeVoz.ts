import { useEffect, useState } from 'react'
import type { EstadoDeVoz } from '../models/catalogo.model'
import { obtenerEstadoDeVoz } from '../services/tablasApi'

const INTERVALO_MS = 4000

/**
 * El SSE de /api/voz/eventos solo avisa "conectado", no si el motor de
 * reconocimiento realmente arrancó. `falla` (el reconocedor no arrancó) y
 * `fallaAlHablar` (la síntesis de la última respuesta fallo) solo salen de
 * /api/voz/estado, asi que se consulta por polling.
 */
export function useEstadoDeVoz() {
  const [estado, setEstado] = useState<EstadoDeVoz | null>(null)

  useEffect(() => {
    let cancelado = false
    const consultar = () => {
      obtenerEstadoDeVoz()
        .then((datos) => { if (!cancelado) setEstado(datos) })
        .catch(() => { if (!cancelado) setEstado(null) })
    }
    consultar()
    const id = setInterval(consultar, INTERVALO_MS)
    return () => { cancelado = true; clearInterval(id) }
  }, [])

  return { estado }
}
