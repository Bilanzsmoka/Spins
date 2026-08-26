import { useState, type ReactNode } from 'react'

export interface Modulo {
  clave: string
  etiqueta: string
  descripcion: string
  /** Los módulos que todavía no existen se muestran, pero no se pueden abrir. */
  disponible: boolean
  contenido?: ReactNode
}

interface Props {
  modulos: Modulo[]
  /** Va en la barra lateral, debajo del menú: el control de voz. */
  panelLateral?: ReactNode
}

/**
 * El armazón de la aplicación: menú a la izquierda, módulo activo a la
 * derecha. Los módulos que todavía no están construidos se listan igual,
 * apagados, para que se vea hacia dónde va esto en vez de aparentar que
 * la aplicación es una sola pantalla.
 */
export function Aplicacion({ modulos, panelLateral }: Props) {
  const primeroDisponible = modulos.find((m) => m.disponible)?.clave ?? ''
  const [activo, setActivo] = useState(primeroDisponible)
  const moduloActivo = modulos.find((m) => m.clave === activo)

  return (
    <div className="aplicacion">
      <aside className="barra-lateral">
        <div className="marca">
          <strong>PokerProOS</strong>
          <span>Spin &amp; Go</span>
        </div>

        <nav className="menu" aria-label="Módulos">
          {modulos.map((modulo) => (
            <button
              key={modulo.clave}
              type="button"
              className={`menu-item${modulo.clave === activo ? ' menu-item-activo' : ''}`}
              disabled={!modulo.disponible}
              aria-current={modulo.clave === activo ? 'page' : undefined}
              onClick={() => setActivo(modulo.clave)}
            >
              <span className="menu-etiqueta">{modulo.etiqueta}</span>
              <span className="menu-descripcion">
                {modulo.disponible ? modulo.descripcion : 'Próximamente'}
              </span>
            </button>
          ))}
        </nav>

        {panelLateral && <div className="lateral-pie">{panelLateral}</div>}
      </aside>

      <main className="contenido">{moduloActivo?.contenido}</main>
    </div>
  )
}
