import type { AccionDefinida, ConsultaRegistrada } from '../../core/models/catalogo.model'

interface Props {
  historial: ConsultaRegistrada[]
  acciones: AccionDefinida[]
  onLimpiar: () => void
}

/**
 * Las respuestas, escritas. Habladas se pierden; escritas quedan, y sirven
 * para repasar la tanda al terminar de jugar y ver qué manos se consultaron
 * más — que suelen ser las que menos se saben.
 */
export function Sugerencias({ historial, acciones, onLimpiar }: Props) {
  const porClave = new Map(acciones.map((a) => [a.clave, a]))

  return (
    <section className="sugerencias">
      <header className="sugerencias-cabecera">
        <h2>Consultas</h2>
        {historial.length > 0 && (
          <button type="button" className="boton-tenue" onClick={onLimpiar}>
            Limpiar
          </button>
        )}
      </header>

      {historial.length === 0 ? (
        <p className="sugerencias-vacio">
          Todavía no dictaste nada. Probá con <em>«siete be be a cinco offsuit»</em>.
        </p>
      ) : (
        <ol className="sugerencias-lista">
          {historial.map((consulta, indice) => {
            const accion = porClave.get(consulta.accion)
            return (
              <li
                key={`${consulta.hora}-${indice}`}
                className={`sugerencia${consulta.resuelta ? '' : ' sugerencia-sin-resolver'}`}
              >
                <div className="sugerencia-linea">
                  {consulta.resuelta ? (
                    <>
                      <strong className="sugerencia-mano">{consulta.manoInterpretada}</strong>
                      <span
                        className="sugerencia-accion"
                        style={accion ? { background: accion.color, color: accion.colorTexto } : undefined}
                      >
                        {accion?.etiqueta ?? consulta.accion}
                      </span>
                      <span className="sugerencia-contexto">
                        {consulta.claveDeStack} · {consulta.spot}
                      </span>
                    </>
                  ) : (
                    // Acá solo llegan consultas de mano: las órdenes de
                    // contexto y lo que no se entendió no entran al historial.
                    // Así que este ramo es una mano que se entendió y no
                    // resolvió —un spot que no existe a ese stack—, y el
                    // motivo es lo único útil que se puede mostrar.
                    <span className="sugerencia-fallo">{consulta.respuesta || 'Sin respuesta'}</span>
                  )}
                  <time className="sugerencia-hora">{consulta.hora}</time>
                </div>

                {/* Lo que se escuchó, para distinguir "no entendió" de
                    "entendió otra cosa y contestó bien la pregunta equivocada". */}
                {consulta.textoCrudo && (
                  <div className="sugerencia-crudo">«{consulta.textoCrudo}»</div>
                )}
                {consulta.resuelta && (
                  <div className="sugerencia-respuesta">{consulta.respuesta}</div>
                )}
              </li>
            )
          })}
        </ol>
      )}
    </section>
  )
}
