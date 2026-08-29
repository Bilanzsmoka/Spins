import { useEffect, useState } from 'react'
import { useCatalogo } from '../../core/hooks/useCatalogo'
import type { GrupoDelGlosario } from '../../core/models/catalogo.model'
import { obtenerGlosario } from '../../core/services/tablasApi'
import { FichaDeJugador } from './FichaDeJugador'

/**
 * Contra quién estás jugando, y qué tablas tenés para cada uno.
 *
 * Los perfiles salen del grupo "jugadores" del glosario y se muestran
 * separados por eje: qué tan fuerte es alguien y cómo juega son dos preguntas
 * distintas —un fish puede ser pasivo o maniaco— y mezclarlas es lo que hace
 * que la clasificación no sirva. Los ejes no están escritos acá: son los que
 * el JSON traiga, en el orden en que aparezcan.
 *
 * La lista de abajo tampoco la escribe nadie: son las etiquetas de tus propias
 * situaciones, que ya dicen contra quién es cada tabla.
 */
export function PaginaDeTiposDeJugador() {
  const { catalogo } = useCatalogo()
  const [grupo, setGrupo] = useState<GrupoDelGlosario | null>(null)

  useEffect(() => {
    let cancelado = false
    obtenerGlosario()
      .then((g) => {
        if (!cancelado) setGrupo(g.find((x) => x.clave === 'jugadores') ?? null)
      })
      .catch(() => { if (!cancelado) setGrupo(null) })
    return () => { cancelado = true }
  }, [])

  const ejes = [...new Set(grupo?.terminos.map((t) => t.eje ?? '') ?? [])]

  return (
    <div className="diccionario">
      <header className="entrenamiento-cabecera">
        <div>
          <h1>Tipos de jugador</h1>
          <p className="subtitulo">A quién tenés enfrente, de un vistazo</p>
        </div>
      </header>

      {ejes.map((eje) => (
        <section key={eje} className="glosario-grupo">
          {eje && <h2>{eje}</h2>}
          <ul className="jugador-lista">
            {grupo!.terminos
              .filter((t) => (t.eje ?? '') === eje)
              .map((t) => (
                <FichaDeJugador key={t.termino} jugador={t} />
              ))}
          </ul>
        </section>
      ))}

      {catalogo && (
        <section className="glosario-grupo">
          <h2>Tus tablas</h2>
          <p className="glosario-nota">
            Cada tabla dice al final contra quién es. Importa porque la misma mano
            se juega distinto según quién esté enfrente.
          </p>
          <ul className="glosario-tablas">
            {catalogo.situaciones.map((s) => (
              <li key={s.clave}>
                <span className="glosario-formato">{s.formato}</span>
                {s.etiqueta}
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  )
}
