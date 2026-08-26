import type { HabitoDefinido, ProgresoDeHabitos } from '../../core/models/catalogo.model'

interface Props {
  progreso: ProgresoDeHabitos
  habitos: HabitoDefinido[]
}

/**
 * La planilla del entrenador, pero calculada. Una fila por hábito, una
 * columna por día. Ver veinte celdas verdes seguidas dice algo que un
 * porcentaje no dice — por eso la grilla va primero y los números después.
 */
export function GrillaDeHabitos({ progreso, habitos }: Props) {
  const soloBinarios = habitos.filter((h) => h.tipo === 'binario')
  const numericos = habitos.filter((h) => h.tipo === 'numero')

  const claseDeCelda = (valor: number | undefined, invertido: boolean) => {
    if (!valor) return 'celda-vacia'
    const bueno = invertido ? valor < 0 : valor > 0
    return bueno ? 'celda-bien' : 'celda-mal'
  }

  const dia = (fecha: string) => fecha.slice(8, 10)

  return (
    <section className="grilla-habitos">
      <h2>Cumplimiento</h2>

      <div className="grilla-scroll">
        <table className="tabla-habitos">
          <thead>
            <tr>
              <th className="col-habito">Hábito</th>
              {progreso.dias.map((d) => (
                <th key={d.fecha} className="col-dia" title={d.fecha}>{dia(d.fecha)}</th>
              ))}
              <th className="col-cifra">Racha</th>
              <th className="col-cifra">Mejor</th>
              <th className="col-cifra">Hechos</th>
            </tr>
          </thead>
          <tbody>
            {soloBinarios.map((habito) => {
              const resumen = progreso.resumen.find((r) => r.clave === habito.clave)
              return (
                <tr key={habito.clave}>
                  <th className="col-habito">
                    {habito.etiqueta}
                    {habito.invertido && <span className="invertido" title="Marcar que sí es lo malo">↓</span>}
                  </th>
                  {progreso.dias.map((d) => {
                    const nota = d.notas[habito.clave]
                    return (
                      <td
                        key={d.fecha}
                        className={claseDeCelda(d.marcas[habito.clave], habito.invertido)}
                        title={nota ? `${d.fecha} — ${nota}` : d.fecha}
                      >
                        {nota && <span className="tiene-nota" aria-label="tiene nota" />}
                      </td>
                    )
                  })}
                  <td className="col-cifra">{resumen?.rachaActual ?? 0}</td>
                  <td className="col-cifra col-tenue">{resumen?.mejorRacha ?? 0}</td>
                  <td className="col-cifra col-tenue">
                    {resumen?.cumplidos ?? 0}/{resumen?.diasRegistrados ?? 0}
                  </td>
                </tr>
              )
            })}

            {/* El nivel de juego va en la misma grilla: es contra esto que se
                cruzan los hábitos, y verlo alineado hace visible el patrón. */}
            <tr className="fila-nivel">
              <th className="col-habito">Jugué en</th>
              {progreso.dias.map((d) => (
                <td key={d.fecha} className="celda-nivel" title={d.fecha}>
                  {d.nivelDeJuego && (
                    <span className={`nivel nivel-${d.nivelDeJuego.toLowerCase()}`}>
                      {d.nivelDeJuego}
                    </span>
                  )}
                </td>
              ))}
              <td colSpan={3} />
            </tr>
          </tbody>
        </table>
      </div>

      {numericos.length > 0 && (
        <div className="totales-numericos">
          {numericos.map((habito) => {
            const total = progreso.dias.reduce((suma, d) => suma + (d.marcas[habito.clave] ?? 0), 0)
            const conDatos = progreso.dias.filter((d) => (d.marcas[habito.clave] ?? 0) > 0).length
            return (
              <div key={habito.clave}>
                <strong>{total}</strong>
                <span>{habito.etiqueta} en {conDatos} días</span>
              </div>
            )
          })}
        </div>
      )}

      <p className="grilla-nota">
        Un día sin marcar no rompe la racha: no marcar no es lo mismo que no
        haberlo hecho. El punto en una celda quiere decir que anotaste qué
        hiciste — pasá el mouse para leerlo.
      </p>
    </section>
  )
}
