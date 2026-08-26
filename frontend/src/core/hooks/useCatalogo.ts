import { useEffect, useState } from 'react'
import type { Catalogo } from '../models/catalogo.model'
import { obtenerCatalogo } from '../services/tablasApi'

export function useCatalogo() {
  const [catalogo, setCatalogo] = useState<Catalogo | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelado = false
    obtenerCatalogo()
      .then((datos) => { if (!cancelado) setCatalogo(datos) })
      .catch((e: unknown) => {
        if (!cancelado) setError(e instanceof Error ? e.message : 'Error desconocido')
      })
    return () => { cancelado = true }
  }, [])

  return { catalogo, error }
}
