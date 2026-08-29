import { useEffect, useState } from 'react'
import type { GrupoDelGlosario } from '../../core/models/catalogo.model'
import { obtenerGlosario } from '../../core/services/tablasApi'
import { FichaDeJugador } from './FichaDeJugador'
import { TerminoConVoz } from './TerminoConVoz'

/**
 * La jerga del juego, explicada y con play.
 *
 * Va aparte de la pantalla de Voz a propósito: aquélla enseña cómo decís vos
 * las cosas para que la app te entienda, ésta enseña qué significan. Son dos
 * problemas distintos y mezclarlos hacía que ninguno se leyera.
 *
 * Los términos salen de database/registro/glosario.json, como todo lo demás:
 * agregar uno es editar el archivo.
 *
 * Un grupo cuyos términos traen figura y color se muestra como fichas —los
 * perfiles de jugador— y no como renglones: si el color se ve en una pantalla
 * y en la otra no, deja de ser una señal y pasa a ser decoración.
 */
export function PaginaDeDiccionario() {
  const [grupos, setGrupos] = useState<GrupoDelGlosario[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelado = false
    obtenerGlosario()
      .then((g) => { if (!cancelado) setGrupos(g) })
      .catch((e) => { if (!cancelado) setError(e instanceof Error ? e.message : 'No se pudo cargar.') })
    return () => { cancelado = true }
  }, [])

  return (
    <div className="diccionario">
      <header className="entrenamiento-cabecera">
        <div>
          <h1>Diccionario</h1>
          <p className="subtitulo">Qué significa cada palabra del juego</p>
        </div>
      </header>

      {error && <p className="sin-entender-error">{error}</p>}
      {grupos?.length === 0 && (
        <p className="cargando">
          No hay términos cargados. Se editan en <code>database/registro/glosario.json</code>.
        </p>
      )}

      {grupos?.map((grupo) => (
        <section key={grupo.clave} className="glosario-grupo">
          <h2>{grupo.titulo}</h2>
          <ul className={grupo.terminos.some((t) => t.icono) ? 'jugador-lista' : 'glosario-lista'}>
            {grupo.terminos.map((t) =>
              t.icono ? (
                <FichaDeJugador key={t.termino} jugador={t} />
              ) : (
                <TerminoConVoz key={t.termino} termino={t.termino} explicacion={t.explicacion} />
              ),
            )}
          </ul>
        </section>
      ))}
    </div>
  )
}
