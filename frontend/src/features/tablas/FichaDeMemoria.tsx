import { useEffect, useState, type ReactNode } from 'react'
import type { AccionDefinida, FichaDeMemoria as FichaModelo } from '../../core/models/catalogo.model'

interface Props {
  ficha: FichaModelo
  acciones: AccionDefinida[]
  guardandoTip: boolean
  errorAlGuardarTip: string | null
  onGuardarTip: (texto: string | null) => void
  onCerrar: () => void
  children?: ReactNode
}

/**
 * Todo lo que se sabe de una mano, en un popup: la casilla con su color y
 * después las relaciones que sirven para memorizarla — hasta dónde llega su
 * familia, desde qué stack cambia, cuánta baraja se lleva y qué pasa después.
 * Reemplaza el "en el borde, N manos" que se hablaba y no decía contra qué.
 */
export function FichaDeMemoria({
  ficha, acciones, guardandoTip, errorAlGuardarTip, onGuardarTip, onCerrar, children,
}: Props) {
  const porClave = new Map(acciones.map((a) => [a.clave, a]))
  const etiqueta = (clave: string) => porClave.get(clave)?.etiqueta ?? clave
  const pintar = (clave: string) => {
    const accion = porClave.get(clave)
    return accion ? { background: accion.color, color: accion.colorTexto } : undefined
  }

  const [editandoTip, setEditandoTip] = useState(false)
  const [borrador, setBorrador] = useState(ficha.tip ?? '')

  // Cambiar de mano sin cerrar el popup (dictando otra) tiene que traer el tip
  // de la nueva, no dejar el borrador de la anterior a medio escribir.
  useEffect(() => {
    // oxlint-disable-next-line set-state-in-effect
    setEditandoTip(false)
    setBorrador(ficha.tip ?? '')
  }, [ficha.mano, ficha.claveDeStack, ficha.tip])

  useEffect(() => {
    const alTeclear = (e: KeyboardEvent) => { if (e.key === 'Escape') onCerrar() }
    window.addEventListener('keydown', alTeclear)
    return () => window.removeEventListener('keydown', alTeclear)
  }, [onCerrar])

  const miPeso = ficha.pesos.find((p) => p.accion === ficha.accion)

  return (
    // El click del fondo cierra; adentro se frena, o cerraría al tocar cualquier cosa.
    <div className="ficha-fondo" onClick={onCerrar} role="presentation">
      <div
        className="ficha-popup"
        role="dialog"
        aria-label={`Ficha de ${ficha.mano}`}
        onClick={(e) => e.stopPropagation()}
      >
        <header className="ficha-cabecera">
          <span className="ficha-casilla" style={pintar(ficha.accion)}>{ficha.mano}</span>
          <div className="ficha-titulo">
            <strong>{etiqueta(ficha.accion)}</strong>
            <span className="ficha-stack">{ficha.claveDeStack}</span>
          </div>
          {miPeso && (
            <span className="ficha-peso">
              {miPeso.porcentajeDeBaraja.toFixed(1)}% de la baraja
            </span>
          )}
          <button type="button" className="boton-tenue" onClick={onCerrar}>Cerrar</button>
        </header>

        {ficha.ancla && (
          <section className="ficha-bloque">
            <h3>Ancla</h3>
            <p>
              En <strong>{ficha.ancla.familia}</strong>, de <strong>{ficha.ancla.tope}</strong>{' '}
              hasta <strong>{ficha.ancla.fondo}</strong> va{' '}
              <span className="ficha-chip" style={pintar(ficha.ancla.accion)}>
                {etiqueta(ficha.ancla.accion)}
              </span>
              {ficha.ancla.siguiente && ficha.ancla.accionSiguiente && (
                <>
                  {'. '}Desde <strong>{ficha.ancla.siguiente}</strong> ya es{' '}
                  <span className="ficha-chip" style={pintar(ficha.ancla.accionSiguiente)}>
                    {etiqueta(ficha.ancla.accionSiguiente)}
                  </span>
                </>
              )}.
            </p>
          </section>
        )}

        {ficha.umbral.length > 0 && (
          <section className="ficha-bloque">
            <h3>Según el stack</h3>
            <ul className="ficha-umbral">
              {ficha.umbral.map((banda) => (
                <li
                  key={banda.claveDeStack}
                  className={banda.esElActual ? 'ficha-banda-actual' : ''}
                >
                  <span className="ficha-banda-stack">{banda.minBB}–{banda.maxBB}bb</span>
                  <span className="ficha-chip" style={pintar(banda.accion)}>
                    {etiqueta(banda.accion)}
                  </span>
                </li>
              ))}
            </ul>
          </section>
        )}

        {ficha.familias.length > 0 && (
          <section className="ficha-bloque">
            <h3>Hasta dónde llega cada familia</h3>
            <ul className="ficha-familias">
              {ficha.familias.map((f) => (
                <li key={f.familia}>
                  <strong>{f.familia}</strong>
                  <span className="ficha-chip" style={pintar(f.accion)}>{etiqueta(f.accion)}</span>
                  hasta <strong>{f.fondo}</strong>
                  {f.siguiente && f.accionSiguiente && (
                    <>
                      {' · '}{f.siguiente}{' '}
                      <span className="ficha-chip" style={pintar(f.accionSiguiente)}>
                        {etiqueta(f.accionSiguiente)}
                      </span>
                    </>
                  )}
                </li>
              ))}
            </ul>
          </section>
        )}

        {ficha.linea.length > 1 && (
          <section className="ficha-bloque">
            <h3>Y después</h3>
            <ol className="ficha-linea">
              {ficha.linea.map((paso) => (
                <li key={paso.spot} className={paso.esElConsultado ? 'ficha-paso-actual' : ''}>
                  <span className="ficha-paso-spot">{paso.etiqueta}</span>
                  <span className="ficha-chip" style={pintar(paso.accion)}>
                    {etiqueta(paso.accion)}
                  </span>
                </li>
              ))}
            </ol>
          </section>
        )}

        <section className="ficha-bloque">
          <h3>Tip</h3>
          {editandoTip ? (
            <div className="ficha-tip-editor">
              <textarea
                value={borrador}
                rows={3}
                placeholder="Por qué esta tabla hace lo que hace"
                onChange={(e) => setBorrador(e.target.value)}
              />
              <div className="ficha-tip-botones">
                <button
                  type="button"
                  disabled={guardandoTip}
                  onClick={() => onGuardarTip(borrador.trim() === '' ? null : borrador)}
                >
                  {guardandoTip ? 'Guardando…' : 'Guardar'}
                </button>
                <button
                  type="button"
                  className="boton-tenue"
                  onClick={() => { setEditandoTip(false); setBorrador(ficha.tip ?? '') }}
                >
                  Cancelar
                </button>
              </div>
              {errorAlGuardarTip && <p className="error">{errorAlGuardarTip}</p>}
            </div>
          ) : (
            <div className="ficha-tip">
              {ficha.tip
                ? <p>{ficha.tip}</p>
                : <p className="ficha-tip-vacio">Todavía no escribiste el porqué de esta tabla.</p>}
              <button type="button" className="boton-tenue" onClick={() => setEditandoTip(true)}>
                {ficha.tip ? 'Editar' : 'Escribir'}
              </button>
            </div>
          )}
        </section>

        {children}
      </div>
    </div>
  )
}
