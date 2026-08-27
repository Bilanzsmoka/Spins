import { useState } from 'react'
import type { AccionDefinida, Celda, ParteDeMix } from '../../core/models/catalogo.model'

interface Props {
  celda: Celda
  acciones: AccionDefinida[]
  guardando: boolean
  onGuardar: (accion: string | null, mix: ParteDeMix[] | null) => void
}

/**
 * Corrige lo que la tabla prescribe para una mano. Escribe en el JSON, que es
 * la fuente de verdad — corregir acá es corregir el dato, no una copia.
 *
 * Vive siempre adentro del popup de la ficha: no tiene su propio "Cerrar" —
 * el del popup (mas Escape y el click en el fondo) ya cierra todo, y un
 * segundo boton con el mismo texto y el mismo efecto solo confunde.
 */
export function EditorDeCelda({ celda, acciones, guardando, onGuardar }: Props) {
  const [modo, setModo] = useState<'pura' | 'mix'>(celda.mix ? 'mix' : 'pura')
  const [primera, setPrimera] = useState(celda.mix?.[0]?.accion ?? celda.accion)
  const [segunda, setSegunda] = useState(celda.mix?.[1]?.accion ?? '')
  const [frecuencia, setFrecuencia] = useState(celda.mix?.[0]?.frecuencia ?? 50)

  const puedeGuardarMix = primera !== '' && segunda !== '' && primera !== segunda

  return (
    <div className="editor-celda" role="dialog" aria-label={`Editar ${celda.mano}`}>
      <header className="editor-cabecera">
        <strong className="editor-mano">{celda.mano}</strong>
        <div className="editor-modos">
          <button type="button" className={modo === 'pura' ? 'modo-activo' : ''}
            onClick={() => setModo('pura')}>Una acción</button>
          <button type="button" className={modo === 'mix' ? 'modo-activo' : ''}
            onClick={() => setModo('mix')}>Mix</button>
        </div>
      </header>

      {modo === 'pura' ? (
        <div className="editor-acciones">
          {acciones.map((accion) => (
            <button
              key={accion.clave}
              type="button"
              className={`editor-accion${celda.mix === null && celda.accion === accion.clave ? ' editor-accion-actual' : ''}`}
              style={{ background: accion.color, color: accion.colorTexto }}
              disabled={guardando}
              onClick={() => onGuardar(accion.clave, null)}
            >
              {accion.etiqueta}
            </button>
          ))}
        </div>
      ) : (
        <div className="editor-mix">
          <div className="editor-mix-fila">
            <select value={primera} onChange={(e) => setPrimera(e.target.value)}>
              <option value="">Elegí una acción</option>
              {acciones.map((a) => <option key={a.clave} value={a.clave}>{a.etiqueta}</option>)}
            </select>
            <input type="number" min={5} max={95} step={5} value={frecuencia}
              onChange={(e) => setFrecuencia(Number(e.target.value))} />
            <span className="editor-porciento">%</span>
          </div>
          <div className="editor-mix-fila">
            <select value={segunda} onChange={(e) => setSegunda(e.target.value)}>
              <option value="">Elegí la segunda</option>
              {acciones.map((a) => <option key={a.clave} value={a.clave}>{a.etiqueta}</option>)}
            </select>
            {/* La segunda frecuencia se deriva: siempre suman 100. */}
            <input type="number" value={100 - frecuencia} disabled />
            <span className="editor-porciento">%</span>
          </div>
          <button
            type="button"
            className="boton-principal"
            disabled={!puedeGuardarMix || guardando}
            onClick={() => onGuardar(null, [
              { accion: primera, frecuencia },
              { accion: segunda, frecuencia: 100 - frecuencia },
            ])}
          >
            {guardando ? 'Guardando…' : 'Guardar mix'}
          </button>
          {primera === segunda && primera !== '' && (
            <p className="aviso-voz">Un mix necesita dos acciones distintas.</p>
          )}
        </div>
      )}
    </div>
  )
}
