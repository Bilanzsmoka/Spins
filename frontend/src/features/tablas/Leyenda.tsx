import type { AccionDefinida, SpotCompleto } from '../../core/models/catalogo.model'

interface Props {
  acciones: AccionDefinida[]
  spot: SpotCompleto
}

/**
 * Solo las acciones que esta tabla usa. El registro tiene doce y una tabla
 * usa dos o cuatro: mostrarlas todas obliga a buscar cuál importa cada vez.
 */
export function Leyenda({ acciones, spot }: Props) {
  // Se toman de las celdas y no de los conteos: una acción que solo aparece
  // como la mitad menor de un mix no figura en los conteos, pero su color sí
  // está en la grilla y por lo tanto tiene que estar en la leyenda.
  const usadas = new Set<string>()
  for (const celda of spot.celdas) {
    if (celda.mix) for (const parte of celda.mix) usadas.add(parte.accion)
    else usadas.add(celda.accion)
  }

  return (
    <div className="leyenda">
      {acciones.filter((a) => usadas.has(a.clave)).map((accion) => (
        <span key={accion.clave} className="leyenda-item">
          <i style={{ backgroundColor: accion.color }} />
          {accion.etiqueta}
          <b>{spot.conteos[accion.clave] ?? 0}</b>
        </span>
      ))}
    </div>
  )
}
