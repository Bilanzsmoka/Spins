import { useCallback, useEffect, useState } from 'react'
import type {
  CategoriaDeVocabulario, FormasHabladas, Vocabulario,
} from '../../core/models/catalogo.model'
import {
  agregarDicho, capturarDictado, obtenerVocabulario, quitarDicho,
} from '../../core/services/tablasApi'

interface Grupo {
  categoria: CategoriaDeVocabulario
  titulo: string
  ayuda: string
  entradas: FormasHabladas[]
}

export function PaginaDeVocabulario() {
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
      const texto = await capturarDictado()
      if (texto === null) setError('No capté nada. Probá de nuevo, más cerca del micrófono.')
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
          <strong>Antes de pelearte con esto</strong>, hacé el entrenamiento de
          voz de Windows: Panel de control → Reconocimiento de voz →
          <em> Entrenar el equipo para que le entienda mejor</em>. Son diez
          minutos y mejora el motor para todo el sistema, no solo para esta app.
        </p>
      </section>
    </div>
  )
}
