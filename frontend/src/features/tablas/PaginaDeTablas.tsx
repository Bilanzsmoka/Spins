import { useEffect, useState } from 'react'
import { useCatalogo } from '../../core/hooks/useCatalogo'
import { useEstadoDeVoz } from '../../core/hooks/useEstadoDeVoz'
import { useEventosDeVoz } from '../../core/hooks/useEventosDeVoz'
import type { SpotCompleto } from '../../core/models/catalogo.model'
import { obtenerSpot } from '../../core/services/tablasApi'
import { AvisoDeProblemas } from './AvisoDeProblemas'
import { EstadoDeVoz } from './EstadoDeVoz'
import { Grilla } from './Grilla'
import { Leyenda } from './Leyenda'
import { Selectores } from './Selectores'

export function PaginaDeTablas() {
  const { catalogo, error } = useCatalogo()
  const { ultimo, conectado } = useEventosDeVoz()
  const { estado } = useEstadoDeVoz()

  const [situacion, setSituacion] = useState('')
  const [stack, setStack] = useState('')
  const [spot, setSpot] = useState('')
  const [datos, setDatos] = useState<SpotCompleto | null>(null)

  // Seleccion inicial: la primera de cada nivel, tomada del catalogo.
  // Sincroniza con un sistema externo (el fetch del catalogo), que es
  // exactamente el caso que useEffect existe para cubrir.
  useEffect(() => {
    if (!catalogo || situacion) return
    const primera = catalogo.situaciones[0]
    if (!primera) return
    const primerStack = primera.stacks[0]
    // oxlint-disable-next-line set-state-in-effect
    setSituacion(primera.clave)
    setStack(primerStack?.clave ?? '')
    setSpot(primerStack?.spots[0]?.clave ?? '')
  }, [catalogo, situacion])

  // La voz manda sobre los selectores: si el dictado trajo stack o spot,
  // la pantalla se mueve a la tabla que se acaba de consultar. Sincroniza
  // con el stream SSE, otro sistema externo.
  useEffect(() => {
    if (!ultimo?.resuelta) return
    // oxlint-disable-next-line set-state-in-effect
    if (ultimo.claveDeStack) setStack(ultimo.claveDeStack)
    if (ultimo.spot) setSpot(ultimo.spot)
    if (ultimo.situacion) setSituacion(ultimo.situacion)
  }, [ultimo])

  // Al cambiar de stack, el spot activo puede no existir ahi (los stacks
  // chicos tienen 3 spots y los demas 5). Caer al primero disponible.
  useEffect(() => {
    if (!catalogo || !situacion || !stack) return
    const stackActivo = catalogo.situaciones
      .find((s) => s.clave === situacion)?.stacks
      .find((t) => t.clave === stack)
    if (stackActivo && !stackActivo.spots.some((p) => p.clave === spot))
      // oxlint-disable-next-line set-state-in-effect
      setSpot(stackActivo.spots[0]?.clave ?? '')
  }, [catalogo, situacion, stack, spot])

  useEffect(() => {
    if (!situacion || !stack || !spot) return
    let cancelado = false
    obtenerSpot(situacion, stack, spot)
      .then((d) => { if (!cancelado) setDatos(d) })
      .catch(() => { if (!cancelado) setDatos(null) })
    return () => { cancelado = true }
  }, [situacion, stack, spot])

  if (error) return <p className="error">No pude cargar el catálogo: {error}</p>
  if (!catalogo) return <p>Cargando…</p>

  // El evento trae el codigo de accion (ALL-IN, CALL...): con eso alcanza
  // para colorear la respuesta con el mismo color que la celda, en vez de
  // adivinarlo leyendo la frase hablada.
  const accionRespondida = catalogo.acciones.find((a) => a.clave === ultimo?.accion)

  return (
    <main className="pagina">
      <h1>Tablas preflop</h1>

      <EstadoDeVoz
        escuchando={estado?.escuchando ?? conectado}
        falla={estado?.falla ?? null}
        fallaAlHablar={estado?.fallaAlHablar ?? null}
        ultimaFrase={ultimo?.textoCrudo ?? estado?.ultimaFrase ?? null}
        manoInterpretada={ultimo?.manoInterpretada || null}
        respuesta={ultimo?.respuesta ?? null}
        colorRespuesta={accionRespondida?.color ?? null}
      />

      <AvisoDeProblemas problemas={catalogo.problemas} />

      <Selectores
        situaciones={catalogo.situaciones}
        situacion={situacion}
        stack={stack}
        spot={spot}
        onSituacion={setSituacion}
        onStack={setStack}
        onSpot={setSpot}
      />

      {datos && (
        <>
          <Grilla
            spot={datos}
            acciones={catalogo.acciones}
            manoResaltada={ultimo?.manoInterpretada || null}
          />
          <Leyenda acciones={catalogo.acciones} conteos={datos.conteos} />
        </>
      )}
    </main>
  )
}
