import { useCallback, useEffect, useState } from 'react'
import type { DiaDeDiario, EntradaDeDiario, HabitoDefinido } from '../../core/models/catalogo.model'
import { guardarDia, listarDiario, obtenerDia, obtenerHabitos } from '../../core/services/tablasApi'
import { AyudaNivelDeJuego } from './AyudaNivelDeJuego'
import { ComparativaConAyer } from './ComparativaConAyer'
import { CuadroDeHabitos } from './CuadroDeHabitos'
import { ResumenDeConsultas } from './ResumenDeConsultas'

const hoy = () => new Date().toLocaleDateString('sv')  // sv da AAAA-MM-DD

const NIVELES = ['A', 'B', 'C'] as const

export function PaginaDeDiario() {
  const [fecha, setFecha] = useState(hoy)
  const [dia, setDia] = useState<DiaDeDiario | null>(null)
  const [entradas, setEntradas] = useState<EntradaDeDiario[]>([])
  const [guardando, setGuardando] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [guardado, setGuardado] = useState(false)

  const [intencion, setIntencion] = useState('')
  const [nivel, setNivel] = useState('')
  const [disparador, setDisparador] = useState('')
  const [mesas, setMesas] = useState('')
  const [minutos, setMinutos] = useState('')
  const [notas, setNotas] = useState('')
  const [objetivo, setObjetivo] = useState('')
  const [cumplimiento, setCumplimiento] = useState('')
  const [habitos, setHabitos] = useState<HabitoDefinido[]>([])
  const [marcas, setMarcas] = useState<Record<string, number>>({})

  const cargar = useCallback((cual: string) => {
    obtenerDia(cual)
      .then((datos) => {
        setDia(datos)
        setIntencion(datos.entrada?.intencion ?? '')
        setNivel(datos.entrada?.nivelDeJuego ?? '')
        setDisparador(datos.entrada?.disparador ?? '')
        setMesas(datos.entrada?.mesas?.toString() ?? '')
        setMinutos(datos.entrada?.minutos?.toString() ?? '')
        setNotas(datos.entrada?.notas ?? '')
        setObjetivo(datos.entrada?.objetivoTecnico ?? '')
        setCumplimiento(datos.entrada?.cumplimientoObjetivo?.toString() ?? '')
        setMarcas(datos.marcas ?? {})
        setError(null)
      })
      .catch((e: unknown) => setError(e instanceof Error ? e.message : 'No pude cargar el día'))
  }, [])

  useEffect(() => { cargar(fecha) }, [fecha, cargar])
  useEffect(() => { obtenerHabitos().then(setHabitos).catch(() => setHabitos([])) }, [])
  useEffect(() => { listarDiario().then(setEntradas).catch(() => setEntradas([])) }, [guardado])

  const guardar = async () => {
    setGuardando(true)
    setError(null)
    try {
      await guardarDia(fecha, {
        intencion: intencion || null,
        nivelDeJuego: nivel || null,
        disparador: disparador || null,
        mesas: mesas ? Number(mesas) : null,
        minutos: minutos ? Number(minutos) : null,
        notas,
        objetivoTecnico: objetivo || null,
        cumplimientoObjetivo: cumplimiento ? Number(cumplimiento) : null,
        habitos: marcas,
      })
      setGuardado((previo) => !previo)
      cargar(fecha)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'No pude guardar')
    } finally {
      setGuardando(false)
    }
  }

  return (
    <div className="diario">
      <header className="entrenamiento-cabecera">
        <div>
          <h1>Diario</h1>
          <p className="subtitulo">Qué hiciste hoy y cómo venís evolucionando</p>
        </div>
        <input
          type="date"
          className="selector-fecha"
          value={fecha}
          max={hoy()}
          onChange={(e) => setFecha(e.target.value)}
        />
      </header>

      {error && <p className="error">{error}</p>}

      {habitos.length > 0 && (
        <CuadroDeHabitos
          habitos={habitos}
          marcas={marcas}
          onCambiar={(clave, valor) =>
            setMarcas((previo) => ({ ...previo, [clave]: valor }))}
        />
      )}

      <div className="diario-cuerpo">
        <section className="diario-entrada">
          <label className="campo">
            <span className="campo-titulo">Objetivo técnico del día</span>
            <span className="campo-ayuda">
              Medible, para poder compararlo mañana. «Bajar el VPIP a 38», no
              «jugar mejor». Es lo que la pantalla te va a recordar el próximo día.
            </span>
            <input
              type="text"
              value={objetivo}
              placeholder="Qué número o qué spot querés mover hoy"
              onChange={(e) => setObjetivo(e.target.value)}
            />
          </label>

          <label className="campo">
            <span className="campo-titulo">Intención del día</span>
            <span className="campo-ayuda">
              Una sola, concreta. No «jugar bien» sino «no pagar all-ins de BB sin blockers».
            </span>
            <input
              type="text"
              value={intencion}
              placeholder="Qué vas a hacer distinto hoy"
              onChange={(e) => setIntencion(e.target.value)}
            />
          </label>

          <div className="campo">
            <span className="campo-titulo">¿Cómo jugaste?</span>
            <div className="niveles">
              {NIVELES.map((letra) => (
                <button
                  key={letra}
                  type="button"
                  className={`nivel-boton nivel-${letra.toLowerCase()}${nivel === letra ? ' nivel-elegido' : ''}`}
                  onClick={() => setNivel(nivel === letra ? '' : letra)}
                >
                  {letra}
                </button>
              ))}
            </div>
            <AyudaNivelDeJuego />
          </div>

          <label className="campo">
            <span className="campo-titulo">¿Qué te disparó?</span>
            <span className="campo-ayuda">
              El disparador, no el tilt. Un bad beat, un multiplicador grande perdido,
              cansancio, un limper que te ganó. El patrón solo aparece si lo anotás.
            </span>
            <input
              type="text"
              value={disparador}
              placeholder="Qué te sacó de tu juego, si pasó"
              onChange={(e) => setDisparador(e.target.value)}
            />
          </label>

          <div className="campo campo-fila">
            <label>
              <span className="campo-titulo">Cumplí el objetivo</span>
              <input type="number" min="1" max="10" value={cumplimiento} placeholder="1-10"
                onChange={(e) => setCumplimiento(e.target.value)} />
            </label>
            <label>
              <span className="campo-titulo">Mesas</span>
              <input type="number" min="1" max="24" value={mesas}
                onChange={(e) => setMesas(e.target.value)} />
            </label>
            <label>
              <span className="campo-titulo">Minutos</span>
              <input type="number" min="0" max="1440" value={minutos}
                onChange={(e) => setMinutos(e.target.value)} />
            </label>
          </div>

          <label className="campo">
            <span className="campo-titulo">Qué pasó hoy</span>
            <span className="campo-ayuda">
              Escribí libre. Para dictar en vez de escribir, tocá acá adentro y
              presioná <kbd>Win</kbd> + <kbd>H</kbd> — es el dictado de Windows,
              distinto del copiloto de manos.
            </span>
            <textarea
              rows={12}
              value={notas}
              placeholder="Cómo arrancaste, qué sentiste, qué notaste, qué querés cambiar mañana…"
              onChange={(e) => setNotas(e.target.value)}
            />
          </label>

          <div className="diario-acciones">
            <button type="button" className="boton-principal" disabled={guardando} onClick={() => void guardar()}>
              {guardando ? 'Guardando…' : 'Guardar el día'}
            </button>
            {dia?.entrada && (
              <span className="diario-sello">
                Guardado {new Date(dia.entrada.actualizadaEn).toLocaleString('es')}
              </span>
            )}
          </div>
        </section>

        <aside className="diario-lateral">
          {dia && <ComparativaConAyer comparativa={dia.comparativa} />}
          {dia && <ResumenDeConsultas resumen={dia.resumen} />}

          <section className="historial-dias">
            <h2>Días anteriores</h2>
            {entradas.length === 0 ? (
              <p className="sugerencias-vacio">Todavía no escribiste ningún día.</p>
            ) : (
              <ul className="historial-lista">
                {entradas.map((entrada) => (
                  <li key={entrada.id}>
                    <button
                      type="button"
                      className={`historial-dia${entrada.fecha === fecha ? ' historial-dia-activo' : ''}`}
                      onClick={() => setFecha(entrada.fecha)}
                    >
                      <span className="historial-fecha">{entrada.fecha}</span>
                      {entrada.nivelDeJuego && (
                        <span className={`nivel nivel-${entrada.nivelDeJuego.toLowerCase()}`}>
                          {entrada.nivelDeJuego}
                        </span>
                      )}
                      <span className="historial-extracto">
                        {entrada.intencion || entrada.notas.slice(0, 60) || '—'}
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </aside>
      </div>
    </div>
  )
}
