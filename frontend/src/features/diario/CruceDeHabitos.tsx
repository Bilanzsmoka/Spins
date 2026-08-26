import type { HabitoDefinido, ProgresoDeHabitos } from '../../core/models/catalogo.model'

interface Props {
  progreso: ProgresoDeHabitos
  habitos: HabitoDefinido[]
}

/**
 * El cruce contra cómo jugaste. Un contador de rachas motiva unos días; esto
 * es evidencia sobre vos mismo, que es lo que de verdad cambia conducta.
 *
 * Los cruces sin suficientes días de los dos lados se muestran igual, pero
 * marcados como todavía sin datos. Mostrar un porcentaje sacado de dos días
 * sería mentir con estadística, y este es el número que va a decidir si el
 * usuario se levanta a meditar.
 */
export function CruceDeHabitos({ progreso, habitos }: Props) {
  const porClave = new Map(habitos.map((h) => [h.clave, h]))

  const cruces = progreso.cruces
    .filter((c) => porClave.get(c.clave)?.tipo === 'binario')
    .filter((c) => c.diasCon + c.diasSin > 0)
    .sort((a, b) => Number(b.confiable) - Number(a.confiable))

  if (cruces.length === 0) return null

  const porcentaje = (buenos: number, total: number) =>
    total === 0 ? null : Math.round((buenos / total) * 100)

  return (
    <section className="cruce">
      <h2>Qué efecto tienen</h2>
      <p className="cruce-intro">
        De los días que registraste tu nivel de juego, cómo jugaste según hayas
        cumplido cada hábito o no. <strong>A o B cuentan como buen día.</strong>
      </p>

      <ul className="cruce-lista">
        {cruces.map((cruce) => {
          const habito = porClave.get(cruce.clave)
          const con = porcentaje(cruce.buenosCon, cruce.diasCon)
          const sin = porcentaje(cruce.buenosSin, cruce.diasSin)
          const diferencia = con !== null && sin !== null ? con - sin : null

          return (
            <li key={cruce.clave} className={cruce.confiable ? '' : 'cruce-flojo'}>
              <div className="cruce-titulo">
                <strong>{habito?.etiqueta ?? cruce.clave}</strong>
                {cruce.confiable && diferencia !== null && (
                  <span className={`cruce-salto ${diferencia >= 0 ? 'mejor' : 'peor'}`}>
                    {diferencia >= 0 ? '+' : ''}{diferencia} puntos
                  </span>
                )}
              </div>

              <div className="cruce-barras">
                <div className="cruce-barra">
                  <span className="cruce-etiqueta">Días que sí ({cruce.diasCon})</span>
                  <div className="barra"><i className="barra-bien" style={{ width: `${con ?? 0}%` }} /></div>
                  <span className="cruce-cifra">{con === null ? '—' : `${con}%`}</span>
                </div>
                <div className="cruce-barra">
                  <span className="cruce-etiqueta">Días que no ({cruce.diasSin})</span>
                  <div className="barra"><i className="barra-mal" style={{ width: `${sin ?? 0}%` }} /></div>
                  <span className="cruce-cifra">{sin === null ? '—' : `${sin}%`}</span>
                </div>
              </div>

              {!cruce.confiable && (
                <p className="cruce-aviso">
                  Todavía sin datos suficientes. Hacen falta al menos 4 días de
                  cada lado —con el hábito y sin él— para que el número
                  signifique algo.
                </p>
              )}
            </li>
          )
        })}
      </ul>

      <p className="cruce-pie">
        Esto es correlación, no causa: puede que medites los días que ya venís
        tranquilo. Aun así, si el salto se sostiene en dos meses, es la mejor
        evidencia que vas a tener sobre vos mismo.
      </p>
    </section>
  )
}
