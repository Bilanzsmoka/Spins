import type { ResumenDelDia } from '../../core/models/catalogo.model'

/**
 * Esto es lo que ningún tracker puede darte. Un tracker registra las manos
 * que jugaste; esto registra las que preguntaste — o sea, las que todavía
 * no sabés. Sale solo de la bitácora del copiloto, sin que haya que
 * anotar nada a mano.
 */
export function ResumenDeConsultas({ resumen }: { resumen: ResumenDelDia }) {
  if (resumen.consultas === 0) {
    return (
      <section className="resumen-dia">
        <h2>Consultas del día</h2>
        <p className="sugerencias-vacio">
          Ese día no consultaste ninguna mano por voz.
        </p>
      </section>
    )
  }

  return (
    <section className="resumen-dia">
      <h2>Consultas del día</h2>

      <div className="resumen-cifras">
        <div>
          <strong>{resumen.consultas}</strong>
          <span>consultas</span>
        </div>
        {resumen.primeraHora && resumen.ultimaHora && (
          <div>
            <strong>{resumen.primeraHora}–{resumen.ultimaHora}</strong>
            <span>franja</span>
          </div>
        )}
        {resumen.consultas > resumen.resueltas && (
          <div>
            <strong>{resumen.consultas - resumen.resueltas}</strong>
            <span>no entendidas</span>
          </div>
        )}
      </div>

      {resumen.manosMasConsultadas.length > 0 && (
        <>
          <p className="resumen-nota">
            Las que más repetiste. Preguntarlas seguido es la señal de que
            todavía no las tenés.
          </p>
          <ul className="resumen-manos">
            {resumen.manosMasConsultadas.map((mano) => (
              <li key={`${mano.mano}-${mano.accion}`}>
                <strong>{mano.mano}</strong>
                <span className="resumen-accion">{mano.accion}</span>
                <span className="resumen-veces">×{mano.veces}</span>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  )
}
