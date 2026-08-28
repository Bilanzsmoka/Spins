import { useCallback, useEffect, useState } from 'react'
import type {
  CategoriaDeVocabulario, FormasHabladas, Vocabulario,
} from '../../core/models/catalogo.model'
import {
  agregarDicho, obtenerVocabulario, quitarDicho,
  type ResultadoDeCaptura,
} from '../../core/services/tablasApi'

/** Por qué no se capturó nada, dicho de forma accionable. */
const MOTIVOS: Record<string, string> = {
  silencio: 'No escuché nada. Hablá más cerca del micrófono y probá de nuevo.',
  'no-speech': 'No escuché nada. Hablá más cerca del micrófono y probá de nuevo.',
  aborted: 'El micrófono estaba ocupado. Probá de nuevo.',
  'audio-capture': 'No encuentro micrófono. Revisá que haya uno conectado.',
  'not-allowed': 'Chrome no me dio permiso para el micrófono.',
  'service-not-allowed': 'Chrome no me dio permiso para el micrófono.',
  network: 'El reconocimiento necesita internet y no hay conexión.',
  'sin-api': 'Este navegador no reconoce voz. Hace falta Chrome o Edge.',
}

interface Grupo {
  categoria: CategoriaDeVocabulario
  titulo: string
  ayuda: string
  entradas: FormasHabladas[]
}

interface Props {
  /**
   * Escucha una frase y devuelve lo que se oyó. Llega de afuera —del mismo
   * hook que tiene el motor continuo— porque el micrófono es uno solo: si
   * esta página abriera el suyo, el que escucha para dictar seguiría corriendo
   * y se llevaría la palabra que se está enseñando.
   */
  onCapturar: () => Promise<ResultadoDeCaptura>
}

export function PaginaDeVocabulario({ onCapturar }: Props) {
  const [vocabulario, setVocabulario] = useState<Vocabulario | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [escuchando, setEscuchando] = useState<string | null>(null)
  const [capturado, setCapturado] = useState<{ clave: string; texto: string } | null>(null)

  const cargar = useCallback(() => {
    obtenerVocabulario()
      .then((v) => { setVocabulario(v); setError(null) })
      .catch((e: unknown) => setError(e instanceof Error ? e.message : 'No pude cargar el vocabulario'))
  }, [])

  useEffect(cargar, [cargar])

  const grabar = async (clave: string) => {
    setEscuchando(clave)
    setCapturado(null)
    setError(null)
    try {
      const { texto, motivo } = await onCapturar()
      // El motivo se muestra en vez de esconderse: "silencio" se arregla
      // hablando mas cerca, "not-allowed" dando permiso y "aborted" es el
      // microfono ocupado. Un mismo mensaje para los tres no deja arreglar
      // ninguno.
      if (texto === null) setError(MOTIVOS[motivo ?? ''] ?? `No capté nada (${motivo}).`)
      else setCapturado({ clave, texto })
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'No pude escuchar')
    } finally {
      setEscuchando(null)
    }
  }

  const confirmar = async (categoria: CategoriaDeVocabulario, clave: string, dicho: string) => {
    try {
      await agregarDicho(categoria, clave, dicho)
      setCapturado(null)
      cargar()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'No pude guardar')
    }
  }

  const borrar = async (categoria: CategoriaDeVocabulario, clave: string, dicho: string) => {
    try {
      await quitarDicho(categoria, clave, dicho)
      cargar()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'No pude borrar')
    }
  }

  if (!vocabulario) {
    return (
      <div>
        <h1>Voz</h1>
        {error ? <p className="error">{error}</p> : <p className="cargando">Cargando…</p>}
      </div>
    )
  }

  const grupos: Grupo[] = [
    {
      categoria: 'Rangos', titulo: 'Rangos', entradas: vocabulario.rangos,
      ayuda: 'Cómo nombrás cada carta. Si decís «ace» y no está, grabalo y queda.',
    },
    {
      categoria: 'Palos', titulo: 'Palos', entradas: vocabulario.palos,
      ayuda: 'Suited y offsuit. Es donde más se equivoca el reconocimiento.',
    },
    {
      categoria: 'Spots', titulo: 'Spots', entradas: vocabulario.spots,
      ayuda: 'Cómo llamás a cada situación de la mano.',
    },
    {
      categoria: 'Situaciones', titulo: 'Situaciones', entradas: vocabulario.situaciones,
      ayuda: 'Cómo pedís cambiar de tabla completa.',
    },
  ]

  return (
    <div className="vocabulario">
      <header className="entrenamiento-cabecera">
        <div>
          <h1>Voz</h1>
          <p className="subtitulo">Enseñale cómo decís vos cada cosa</p>
        </div>
      </header>

      <section className="explicacion">
        <p>
          El copiloto entiende una lista cerrada de palabras — por eso es rápido
          y no inventa. Si decís algo que no está en la lista, <strong>no te
          entiende mal: te ignora</strong>.
        </p>
        <p>
          Apretá <strong>Grabar</strong> al lado de lo que quieras enseñarle y
          decilo como lo dirías jugando. La app anota cómo sonó y lo agrega. No
          importa que lo escriba raro — lo que importa es que lo escuche igual
          la próxima vez.
        </p>
      </section>

      {error && <p className="error">{error}</p>}

      {grupos.map((grupo) => (
        <section key={grupo.categoria} className="grupo-vocabulario">
          <h2>{grupo.titulo}</h2>
          <p className="grupo-ayuda">{grupo.ayuda}</p>

          <ul className="lista-vocabulario">
            {grupo.entradas.map((entrada) => (
              <li key={entrada.clave}>
                <div className="entrada-cabecera">
                  <strong className="entrada-clave">{entrada.clave}</strong>
                  <button
                    type="button"
                    className="boton-grabar"
                    disabled={escuchando !== null}
                    onClick={() => void grabar(entrada.clave)}
                  >
                    {escuchando === entrada.clave ? 'Escuchando…' : 'Grabar'}
                  </button>
                </div>

                <div className="dichos">
                  {entrada.dichos.map((dicho) => (
                    <span key={dicho} className="dicho">
                      {dicho}
                      <button
                        type="button"
                        aria-label={`Quitar ${dicho}`}
                        onClick={() => void borrar(grupo.categoria, entrada.clave, dicho)}
                      >
                        ×
                      </button>
                    </span>
                  ))}
                </div>

                {capturado?.clave === entrada.clave && (
                  <div className="capturado">
                    <span>Escuché: <strong>«{capturado.texto}»</strong></span>
                    <button
                      type="button"
                      className="boton-principal"
                      onClick={() => void confirmar(grupo.categoria, entrada.clave, capturado.texto)}
                    >
                      Agregar
                    </button>
                    <button type="button" className="boton-tenue" onClick={() => setCapturado(null)}>
                      Descartar
                    </button>
                  </div>
                )}
              </li>
            ))}
          </ul>
        </section>
      ))}

      <section className="grupo-vocabulario">
        <h2>Palabras de stack</h2>
        <p className="grupo-ayuda">
          Lo que decís antes del número: «siete <em>be be</em> a cinco». No se
          graban de a una porque no pertenecen a una clave: son la lista entera.
        </p>
        <div className="dichos">
          {vocabulario.palabrasDeStack.map((palabra) => (
            <span key={palabra} className="dicho">{palabra}</span>
          ))}
        </div>
      </section>

      <section className="explicacion">
        <p>
          <strong>Quien escucha es el navegador</strong>, no Windows. El
          entrenamiento de voz de Windows no cambia nada acá: Chrome usa su
          propio reconocimiento, que no se entrena a mano.
        </p>
        <p>
          Lo que sí lo mejora es esta pantalla. Si una palabra no se entiende,
          grabala acá y agregá la forma que salga: el reconocimiento propone
          lo que oyó, y esa forma pasa a valer. Las palabras de una sílaba
          («as», «rey», «tres») son las que más lo necesitan, porque sueltas se
          confunden con cualquier cosa; decirlas dentro de la frase entera
          («as rey offsuit») se reconoce mucho mejor que decirlas solas.
        </p>
      </section>
    </div>
  )
}
