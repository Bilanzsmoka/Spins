import type { SituacionResumen } from '../../core/models/catalogo.model'

interface Props {
  situaciones: SituacionResumen[]
  situacion: string
  stack: string
  spot: string
  onSituacion: (clave: string) => void
  onStack: (clave: string) => void
  onSpot: (clave: string) => void
}

export function Selectores({
  situaciones, situacion, stack, spot, onSituacion, onStack, onSpot,
}: Props) {
  // Todas las opciones salen del catalogo. No hay listas en el front.
  const situacionActiva = situaciones.find((s) => s.clave === situacion)
  const stackActivo = situacionActiva?.stacks.find((t) => t.clave === stack)

  return (
    <div className="selectores">
      <label>
        Situación
        <select value={situacion} onChange={(e) => onSituacion(e.target.value)}>
          {situaciones.map((s) => (
            <option key={s.clave} value={s.clave}>{s.etiqueta}</option>
          ))}
        </select>
      </label>
      <label>
        Stack
        <select value={stack} onChange={(e) => onStack(e.target.value)}>
          {situacionActiva?.stacks.map((t) => (
            <option key={t.clave} value={t.clave}>{t.clave}</option>
          ))}
        </select>
      </label>
      <label>
        Spot
        <select value={spot} onChange={(e) => onSpot(e.target.value)}>
          {stackActivo?.spots.map((p) => (
            <option key={p.clave} value={p.clave}>{p.etiqueta}</option>
          ))}
        </select>
      </label>
    </div>
  )
}
