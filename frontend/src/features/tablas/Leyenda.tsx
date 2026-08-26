import type { AccionDefinida } from '../../core/models/catalogo.model'

export function Leyenda({ acciones, conteos }: {
  acciones: AccionDefinida[]
  conteos: Record<string, number>
}) {
  return (
    <div className="leyenda">
      {/* Se arma sola con lo que declare el registro. */}
      {acciones.map((accion) => (
        <span key={accion.clave} className="leyenda-item">
          <i style={{ backgroundColor: accion.color }} />
          {accion.etiqueta}
          <b>{conteos[accion.clave] ?? 0}</b>
        </span>
      ))}
    </div>
  )
}
