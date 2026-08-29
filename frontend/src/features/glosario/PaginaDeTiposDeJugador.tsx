import { useEffect, useState } from 'react'
import { useCatalogo } from '../../core/hooks/useCatalogo'
import type { GrupoDelGlosario } from '../../core/models/catalogo.model'
import { obtenerGlosario } from '../../core/services/tablasApi'
import { TerminoConVoz } from './TerminoConVoz'

/**
 * Contra quién estás jugando, y qué tablas tenés para cada uno.
 *
 * Los términos salen del grupo "jugadores" del glosario. La lista de abajo no
 * la escribe nadie: son las etiquetas de tus propias situaciones, que ya
 * dicen contra quién es cada tabla. Se muestran tal cual — el código no
 * interpreta la etiqueta ni deduce nada de la clave, sólo la enseña.
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

  return (
    <div className="diccionario">
      <header className="entrenamiento-cabecera">
        <div>
          <h1>Tipos de jugador</h1>
          <p className="subtitulo">Contra quién es cada tabla</p>
        </div>
      </header>

      {grupo && (
        <section className="glosario-grupo">
          <ul className="glosario-lista">
            {grupo.terminos.map((t) => (
              <TerminoConVoz key={t.termino} termino={t.termino} explicacion={t.explicacion} />
            ))}
          </ul>
        </section>
      )}

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
