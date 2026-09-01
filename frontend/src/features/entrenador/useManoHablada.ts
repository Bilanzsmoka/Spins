import { useEffect, useState } from 'react'
import type { Vocabulario } from '../../core/models/catalogo.model'
import { obtenerVocabulario } from '../../core/services/tablasApi'

/**
 * Cómo se dice una mano en voz alta: "A K offsuit" y no "AKo".
 *
 * Deletreada, porque la síntesis lee la etiqueta pegada como una palabra
 * inventada. La palabra de cada palo sale de `vocabulario.palos` —su forma
 * canónica, el primer dicho—, no de un literal: un diccionario
 * `{ s: 'suited', o: 'offsuit' }` acá sería la excepción que el proyecto
 * prohíbe.
 *
 * Vive aparte porque lo necesitan dos: el que canta el fallo y, si algún día
 * vuelve a hacer falta, el que canta la pregunta. Sin el vocabulario cargado
 * devuelve la mano sin el palo antes que inventarlo.
 */
export function useManoHablada() {
  const [vocabulario, setVocabulario] = useState<Vocabulario | null>(null)

  useEffect(() => {
    let cancelado = false
    obtenerVocabulario()
      .then((v) => { if (!cancelado) setVocabulario(v) })
      .catch(() => { if (!cancelado) setVocabulario(null) })
    return () => { cancelado = true }
  }, [])

  return (mano: string) => {
    const palo = mano.length > 2
      ? vocabulario?.palos.find((p) => p.clave === mano[2])?.dichos[0] ?? ''
      : ''
    return `${mano[0]} ${mano[1]} ${palo}`.trim()
  }
}
