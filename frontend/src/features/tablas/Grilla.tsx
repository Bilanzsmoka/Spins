import type { AccionDefinida, SpotCompleto } from '../../core/models/catalogo.model'
import { Celda } from './Celda'

const RANGOS = ['A', 'K', 'Q', 'J', 'T', '9', '8', '7', '6', '5', '4', '3', '2']

interface Props {
  spot: SpotCompleto
  acciones: AccionDefinida[]
  manoResaltada: string | null
  /** En modo edicion, tocar una celda la abre para corregir. */
  onTocarCelda?: (mano: string) => void
}

function etiqueta(fila: number, columna: number): string {
  const alto = RANGOS[Math.min(fila, columna)]
  const bajo = RANGOS[Math.max(fila, columna)]
  if (fila === columna) return `${alto}${bajo}`
  return fila < columna ? `${alto}${bajo}s` : `${alto}${bajo}o`
}

export function Grilla({ spot, acciones, manoResaltada, onTocarCelda }: Props) {
  const porMano = new Map(spot.celdas.map((c) => [c.mano, c]))
  const porClave = new Map(acciones.map((a) => [a.clave, a]))

  return (
    <div className="grilla">
      <div className="grilla-fila">
        <div className="grilla-esquina" />
        {RANGOS.map((r) => <div key={r} className="grilla-encabezado">{r}</div>)}
      </div>
      {RANGOS.map((rangoFila, fila) => (
        <div key={rangoFila} className="grilla-fila">
          <div className="grilla-encabezado">{rangoFila}</div>
          {RANGOS.map((_, columna) => {
            const mano = etiqueta(fila, columna)
            const celda = porMano.get(mano)
            return (
              <Celda
                key={mano}
                mano={mano}
                accion={porClave.get(celda?.accion ?? '')}
                mix={celda?.mix ?? null}
                acciones={acciones}
                resaltada={mano === manoResaltada}
                onTocar={onTocarCelda}
              />
            )
          })}
        </div>
      ))}
    </div>
  )
}
