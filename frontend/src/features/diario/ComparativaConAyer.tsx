import type { Comparativa } from '../../core/models/catalogo.model'

/**
 * El bucle que hace útil al objetivo: proponerte algo un día no sirve si al
 * día siguiente nadie te lo recuerda. Acá se ve qué te propusiste la última
 * vez que jugaste, cómo lo calificaste, y cómo viene hoy contra ese día.
 *
 * Compara contra el último día con entrada, no contra ayer: si el sábado no
 * jugaste, el domingo se compara contra el viernes.
 */
export function ComparativaConAyer({ comparativa }: { comparativa: Comparativa }) {
  const {
    fechaPrevia, objetivoPrevio, cumplimientoPrevio, nivelPrevio,
    volumenPrevio, volumenDeHoy, consultasPrevias, consultasDeHoy,
  } = comparativa

  if (!fechaPrevia) {
    return (
      <section className="comparativa">
        <h2>Comparación</h2>
        <p className="sugerencias-vacio">
          Es tu primer día registrado. Mañana vas a ver acá qué te propusiste hoy
          y cómo venís contra este día.
        </p>
      </section>
    )
  }

  const diferencia = (hoy: number | null, previo: number | null) => {
    if (hoy === null || previo === null) return null
    return hoy - previo
  }

  const volumen = diferencia(volumenDeHoy, volumenPrevio)
  const consultas = consultasDeHoy - consultasPrevias

  return (
    <section className="comparativa">
      <h2>Contra el {fechaPrevia}</h2>

      {objetivoPrevio ? (
        <div className="comparativa-objetivo">
          <span className="campo-titulo">Te propusiste</span>
          <p>«{objetivoPrevio}»</p>
          {cumplimientoPrevio !== null && (
            <span className="comparativa-nota">
              Lo calificaste {cumplimientoPrevio} de 10
              {nivelPrevio && <> · jugaste en <strong>{nivelPrevio}</strong></>}
            </span>
          )}
        </div>
      ) : (
        <p className="sugerencias-vacio">
          Ese día no anotaste objetivo técnico. Es el campo que hace que esta
          comparación sirva.
        </p>
      )}

      <ul className="comparativa-lineas">
        {volumen !== null && (
          <li>
            <span>Volumen</span>
            <strong className={volumen >= 0 ? 'mejor' : 'peor'}>
              {volumen >= 0 ? '+' : ''}{volumen}
            </strong>
            <span className="comparativa-crudo">{volumenPrevio} → {volumenDeHoy}</span>
          </li>
        )}
        <li>
          <span>Consultas por voz</span>
          <strong className={consultas <= 0 ? 'mejor' : 'peor'}>
            {consultas >= 0 ? '+' : ''}{consultas}
          </strong>
          <span className="comparativa-crudo">{consultasPrevias} → {consultasDeHoy}</span>
        </li>
      </ul>

      <p className="comparativa-pie">
        Consultar menos la misma tabla es la señal de que la estás aprendiendo.
        Consultar más no es malo si estás en tablas nuevas.
      </p>
    </section>
  )
}
