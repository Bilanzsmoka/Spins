import { useEffect, useState } from 'react'
import type { HabitoDefinido, ProgresoDeHabitos } from '../../core/models/catalogo.model'
import { obtenerHabitos, obtenerProgreso } from '../../core/services/tablasApi'
import { CruceDeHabitos } from './CruceDeHabitos'
import { GrillaDeHabitos } from './GrillaDeHabitos'

const PERIODOS = [
  { dias: 14, etiqueta: '2 semanas' },
  { dias: 30, etiqueta: '30 días' },
  { dias: 90, etiqueta: '3 meses' },
]

export function PaginaDeHabitos() {
  const [dias, setDias] = useState(30)
  const [progreso, setProgreso] = useState<ProgresoDeHabitos | null>(null)
  const [habitos, setHabitos] = useState<HabitoDefinido[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => { obtenerHabitos().then(setHabitos).catch(() => setHabitos([])) }, [])

  useEffect(() => {
    obtenerProgreso(dias)
      .then((datos) => { setProgreso(datos); setError(null) })
      .catch((e: unknown) => setError(e instanceof Error ? e.message : 'No pude cargar el progreso'))
  }, [dias])

  const conDatos = progreso?.dias.some((d) => Object.keys(d.marcas).length > 0) ?? false

  return (
    <div className="habitos-pagina">
      <header className="entrenamiento-cabecera">
        <div>
          <h1>Hábitos</h1>
          <p className="subtitulo">Cómo los venís cumpliendo y qué efecto tienen</p>
        </div>
        <div className="periodos">
          {PERIODOS.map((periodo) => (
            <button
              key={periodo.dias}
              type="button"
              className={`periodo${dias === periodo.dias ? ' periodo-activo' : ''}`}
              onClick={() => setDias(periodo.dias)}
            >
              {periodo.etiqueta}
            </button>
          ))}
        </div>
      </header>

      {error && <p className="error">{error}</p>}

      {!conDatos ? (
        <p className="sugerencias-vacio">
          Todavía no marcaste hábitos. Empezá en el <strong>Diario</strong>: marcá
          lo que hiciste hoy y en unos días esta pantalla va a tener algo que
          mostrarte.
        </p>
      ) : progreso && (
        <>
          <GrillaDeHabitos progreso={progreso} habitos={habitos} />
          <CruceDeHabitos progreso={progreso} habitos={habitos} />
        </>
      )}
    </div>
  )
}
