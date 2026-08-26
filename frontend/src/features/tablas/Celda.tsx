import type { AccionDefinida } from '../../core/models/catalogo.model'

interface Props {
  mano: string
  accion: AccionDefinida | undefined
  resaltada: boolean
}

export function Celda({ mano, accion, resaltada }: Props) {
  return (
    <div
      className={`celda${resaltada ? ' celda-resaltada' : ''}`}
      style={{
        backgroundColor: accion?.color ?? 'var(--desconocido)',
        color: accion?.colorTexto ?? 'var(--texto)',
      }}
      title={`${mano}: ${accion?.etiqueta ?? 'desconocida'}`}
    >
      {/* El color nunca es la unica senal: la etiqueta va siempre visible. */}
      {mano}
    </div>
  )
}
