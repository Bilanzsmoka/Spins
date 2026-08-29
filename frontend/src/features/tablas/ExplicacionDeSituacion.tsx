import { useEffect, useState } from 'react'
import type { SituacionResumen } from '../../core/models/catalogo.model'

interface Props {
  situaciones: SituacionResumen[]
}

/**
 * Cada cuánto cambia sola. Diez minutos es largo a propósito: no viene a
 * competir con la tabla que estás mirando, viene a que en una sesión larga
 * pasen cuatro o cinco situaciones por delante tuyo sin que las busques.
 */
const CADA = 10 * 60 * 1000

/**
 * Una situación al azar, explicada.
 *
 * Memorizar las tablas no sirve de nada si no reconocés en qué situación
 * estás sentado, y eso no se aprende mirando una grilla: la grilla ya asume
 * que sabés qué es "BB vs 3-way limp". El texto lo declara cada archivo de
 * tabla a mano — ningún cálculo puede deducir qué pasó en la mesa.
 *
 * Al azar y no en orden porque el orden se memoriza como orden: a la tercera
 * vuelta reconocerías la que sigue por su posición en la lista y no por lo
 * que dice.
 */
export function ExplicacionDeSituacion({ situaciones }: Props) {
  const conTexto = situaciones.filter((s) => s.explicacion)
  const [cual, setCual] = useState(() => Math.floor(Math.random() * Math.max(conTexto.length, 1)))

  // Otra distinta a la que está: repetir la misma al cambiar de sola haría
  // que el temporizador pareciera roto.
  const otra = () => setCual((previo) => conTexto.length < 2
    ? previo
    : (previo + 1 + Math.floor(Math.random() * (conTexto.length - 1))) % conTexto.length)

  useEffect(() => {
    if (conTexto.length < 2) return
    const reloj = setInterval(otra, CADA)
    return () => clearInterval(reloj)
    // oxlint-disable-next-line exhaustive-deps
  }, [conTexto.length])

  if (conTexto.length === 0) return null
  const situacion = conTexto[cual % conTexto.length]

  return (
    <section className="explicacion" aria-live="polite">
      <header className="explicacion-cabecera">
        <span className="explicacion-etiqueta">{situacion.etiqueta}</span>
        <button type="button" className="boton-tenue" onClick={otra}>
          Otra
        </button>
      </header>
      <p className="explicacion-texto">{situacion.explicacion}</p>
    </section>
  )
}
