import { useContext, useEffect, useState } from 'react'
import type { EstadoDeHito, EstadoDelDia } from '../../core/models/catalogo.model'
import { obtenerPlanDeHoy } from '../../core/services/tablasApi'
import type { FocoDeEntrenamiento } from '../entrenador/foco'
import { IrAlModulo } from '../../shared/IrAlModulo'

interface Props {
  /** Deja anotada la tabla que toca, para que el entrenador arranque en ella. */
  onEntrenar: (foco: FocoDeEntrenamiento) => void
}

const DIAS = ['D', 'L', 'M', 'M', 'J', 'V', 'S']

/**
 * Cuánto lleva el hito, dicho en lo que ese hito mide. Un hito de saber va en
 * bordes y su porcentaje contra el objetivo; uno de jugar va en días que
 * llegaste, y ahí el objetivo son torneos por día — meterlo en la misma frase
 * daba "0 de 140%", que no significa nada.
 */
function Cuanto({ hito }: { hito: EstadoDeHito }) {
  return hito.tipo === 'saber' ? (
    <span className="hoy-cifra">
      {hito.hecho} / {hito.total} bordes<b> · {hito.porcentaje} de {hito.objetivo}%</b>
    </span>
  ) : (
    <span className="hoy-cifra">
      {hito.hecho} / {hito.total} días<b> · {hito.objetivo} por día</b>
    </span>
  )
}

function Barra({ porcentaje, apagada = false }: { porcentaje: number; apagada?: boolean }) {
  return (
    <div className={`hoy-barra${apagada ? ' hoy-barra-apagada' : ''}`}>
      <i style={{ width: `${Math.min(100, Math.max(0, porcentaje))}%` }} />
    </div>
  )
}

/**
 * La única pregunta que hay que poder contestar todos los días: ¿hoy voy bien?
 *
 * Va arriba de todo en Hábitos y no en su propia pantalla porque es lo mismo
 * que el resto del cuadro: cosas que se miran una vez por día. Si necesita
 * scroll para leerse, falló.
 */
export function PanelDeHoy({ onEntrenar }: Props) {
  const irA = useContext(IrAlModulo)
  const [estado, setEstado] = useState<EstadoDelDia | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelado = false
    obtenerPlanDeHoy()
      .then((r) => { if (!cancelado) { setEstado(r.estado ?? null); setError(null) } })
      .catch((e: unknown) => {
        if (!cancelado) setError(e instanceof Error ? e.message : 'No pude leer tu plan')
      })
    return () => { cancelado = true }
  }, [])

  // Sin plan no hay panel y la pantalla queda como estaba: no escribiste uno
  // todavía, que no es un error.
  if (error) return <p className="error">{error}</p>
  if (!estado) return null

  const activo = estado.hitos.find((h) => h.esElActivo)
  const hayHistorial = estado.semana.some((d) => d.volumen > 0)
  const rotos = estado.hitos.filter((h) => h.problema)
  const volumen = estado.metaDeVolumen > 0
    ? (100 * estado.volumenDeHoy) / estado.metaDeVolumen
    : 0

  const entrenar = () => {
    if (estado.situacionQueToca) onEntrenar({ situacion: estado.situacionQueToca })
    irA('entrenador')
  }

  return (
    <section className="panel-de-hoy">
      <div className="hoy-fila">
        <span className="hoy-etiqueta">Volumen</span>
        <Barra porcentaje={volumen} />
        <span className="hoy-cifra">
          {estado.volumenDeHoy} / {estado.metaDeVolumen} torneos
        </span>
      </div>

      <div className="hoy-fila">
        <span className="hoy-etiqueta">Estudio</span>
        <span className={`hoy-marca${estado.estudioHecho ? ' hoy-marca-hecha' : ''}`}>
          {estado.estudioHecho ? 'hecho' : 'todavía no'}
        </span>
      </div>

      {activo && (
        <div className="hoy-hito">
          <span className="hoy-etiqueta">Hito activo</span>
          <strong className="hoy-titulo">{activo.titulo}</strong>
          <Barra porcentaje={activo.porcentaje} />
          <Cuanto hito={activo} />
          {estado.situacionQueToca && (
            <button type="button" className="boton-principal" onClick={entrenar}>
              Entrenar
            </button>
          )}
        </div>
      )}

      <div className="hoy-semana">
        {estado.semana.map((dia) => (
          <span
            key={dia.fecha}
            className={`hoy-dia${dia.alcanzo ? ' hoy-dia-ok' : ''}${dia.esHoy ? ' hoy-dia-hoy' : ''}`}
            title={`${dia.fecha}: ${dia.volumen}`}
          >
            {DIAS[new Date(`${dia.fecha}T00:00:00`).getDay()]}
          </span>
        ))}
        {/*
          No se muestra racha a propósito: medir días seguidos hace abandonar el
          hábito entero al primer fallo. Lo que se sostiene es no fallar dos
          veces seguidas, y eso es lo que dice acá.
        */}
        {hayHistorial ? (
          <span className={`hoy-regla${estado.sinDosSeguidos ? '' : ' hoy-regla-rota'}`}>
            {estado.sinDosSeguidos ? 'sin dos días seguidos' : 'dos días seguidos sin llegar'}
          </span>
        ) : (
          <span className="hoy-regla">anotá tu volumen en el Diario</span>
        )}
      </div>

      {rotos.length > 0 && (
        <ul className="hoy-problemas">
          {rotos.map((h) => (
            <li key={h.clave}>
              <strong>{h.titulo}</strong> — {h.problema}
            </li>
          ))}
        </ul>
      )}

      <details className="hoy-todos">
        <summary>
          El plan entero · {estado.hitos.filter((h) => h.cumplido).length} de {estado.hitos.length}
        </summary>
        <ol>
          {estado.hitos.map((h) => (
            <li key={h.clave} className={h.cumplido ? 'hoy-cumplido' : undefined}>
              <span>{h.titulo}</span>
              <b>{h.problema ? '—' : `${h.porcentaje}%`}</b>
            </li>
          ))}
        </ol>
      </details>
    </section>
  )
}
