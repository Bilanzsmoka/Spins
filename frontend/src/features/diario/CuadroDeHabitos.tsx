import { useState } from 'react'
import type { HabitoDefinido } from '../../core/models/catalogo.model'

interface Props {
  habitos: HabitoDefinido[]
  marcas: Record<string, number>
  notas: Record<string, string>
  onCambiar: (clave: string, valor: number) => void
  onNota: (clave: string, nota: string) => void
}

/**
 * El cuadro diario del entrenador. Cada hábito se marca hecho o no hecho;
 * el volumen es un número. Los hábitos salen del registro en datos, así que
 * agregar uno nuevo no toca este componente.
 */
export function CuadroDeHabitos({ habitos, marcas, notas, onCambiar, onNota }: Props) {
  const [ayudaAbierta, setAyudaAbierta] = useState<string | null>(null)
  const habitoConAyuda = habitos.find((h) => h.clave === ayudaAbierta)

  return (
    <section className="cuadro-habitos">
      <div className="campo-titulo">Hábitos del día</div>

      <div className="habitos-fila">
        {habitos.map((habito) => {
          const valor = marcas[habito.clave] ?? 0
          const esNumero = habito.tipo === 'numero'

          return (
            <div key={habito.clave} className="habito">
              <button
                type="button"
                className="habito-etiqueta"
                title="Ver para qué sirve"
                onClick={() => setAyudaAbierta(ayudaAbierta === habito.clave ? null : habito.clave)}
              >
                {habito.etiqueta}
              </button>

              {esNumero ? (
                <input
                  type="number"
                  className="habito-numero"
                  min="0"
                  value={valor || ''}
                  placeholder="0"
                  onChange={(e) => onCambiar(habito.clave, Number(e.target.value) || 0)}
                />
              ) : (
                <div className="habito-botones">
                  {/* Marcar que sí en un hábito invertido (el tilt) es lo malo:
                      por eso el color se elige según `invertido`, no según el signo. */}
                  <button
                    type="button"
                    className={`habito-si${valor === 1 ? (habito.invertido ? ' marcado-malo' : ' marcado-bueno') : ''}`}
                    onClick={() => onCambiar(habito.clave, valor === 1 ? 0 : 1)}
                  >
                    Sí
                  </button>
                  <button
                    type="button"
                    className={`habito-no${valor === -1 ? (habito.invertido ? ' marcado-bueno' : ' marcado-malo') : ''}`}
                    onClick={() => onCambiar(habito.clave, valor === -1 ? 0 : -1)}
                  >
                    No
                  </button>
                </div>
              )}
            </div>
          )
        })}
      </div>

      {/* Al marcar que si aparece una linea para anotar QUE hiciste. Opcional:
          exigirla mataria el habito de marcar, pero "medite 10 min antes de
          jugar" vale mucho mas que un tilde suelto cuando lo releas en un mes. */}
      {habitos.filter((h) => h.tipo === 'binario' && (marcas[h.clave] ?? 0) === 1).map((habito) => (
        <label key={habito.clave} className="habito-nota-campo">
          <span className="campo-titulo">{habito.etiqueta} — ¿qué hiciste?</span>
          <input
            type="text"
            value={notas[habito.clave] ?? ''}
            placeholder="Opcional. Una línea alcanza."
            onChange={(e) => onNota(habito.clave, e.target.value)}
          />
        </label>
      ))}

      {habitoConAyuda && (
        <div className="habito-ayuda">
          <strong>{habitoConAyuda.etiqueta}</strong>
          <p>{habitoConAyuda.ayuda}</p>
        </div>
      )}
    </section>
  )
}
