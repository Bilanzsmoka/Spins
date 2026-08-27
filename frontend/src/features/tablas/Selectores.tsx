import type { SituacionResumen } from '../../core/models/catalogo.model'

interface Props {
  situaciones: SituacionResumen[]
  situacion: string
  stack: string
  spot: string
  onFormato: (formato: string) => void
  onSituacion: (clave: string) => void
  onStack: (clave: string) => void
  onSpot: (clave: string) => void
}

export function Selectores({
  situaciones, situacion, stack, spot, onFormato, onSituacion, onStack, onSpot,
}: Props) {
  // Todas las opciones salen del catalogo. No hay listas en el front.
  const situacionActiva = situaciones.find((s) => s.clave === situacion)
  const stackActivo = situacionActiva?.stacks.find((t) => t.clave === stack)

  // El formato no es estado propio: es el de la situacion activa. Asi un
  // dictado que cambia de situacion mueve este selector solo, en vez de
  // quedar peleado con lo que se eligio a mano.
  const formatoActivo = situacionActiva?.formato ?? ''
  const formatos = [...new Set(situaciones.map((s) => s.formato))]
  const deEsteFormato = situaciones.filter((s) => s.formato === formatoActivo)

  return (
    <div className="selectores">
      <label>
        Formato
        <select value={formatoActivo} onChange={(e) => onFormato(e.target.value)}>
          {formatos.map((f) => (
            <option key={f} value={f}>{f}</option>
          ))}
        </select>
      </label>
      <label>
        Situación
        <select value={situacion} onChange={(e) => onSituacion(e.target.value)}>
          {deEsteFormato.map((s) => (
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
