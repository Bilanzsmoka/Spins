import { useState, type ReactNode } from 'react'

export interface Modulo {
  clave: string
  etiqueta: string
  descripcion: string
  /** Los módulos que todavía no existen se muestran, pero no se pueden abrir. */
  disponible: boolean
  contenido?: ReactNode
}

export interface GrupoDeModulos {
  clave: string
  etiqueta: string
  modulos: Modulo[]
}

interface Props {
  grupos: GrupoDeModulos[]
}

/**
 * El armazón: menú a la izquierda agrupado por área, módulo activo a la
 * derecha. Los módulos que todavía no están construidos se listan igual,
 * apagados, para que se vea hacia dónde va esto en vez de aparentar que la
 * aplicación es una sola pantalla.
 */
export function Aplicacion({ grupos }: Props) {
  const todos = grupos.flatMap((g) => g.modulos)
  const [activo, setActivo] = useState(todos.find((m) => m.disponible)?.clave ?? '')
  const moduloActivo = todos.find((m) => m.clave === activo)

  return (
    <div className="aplicacion">
      <aside className="barra-lateral">
        <div className="marca">
          <strong>PokerProOS</strong>
          <span>Spin &amp; Go</span>
        </div>

        <nav className="menu" aria-label="Módulos">
          {grupos.map((grupo) => (
            <div key={grupo.clave} className="menu-grupo">
              <p className="menu-grupo-titulo">{grupo.etiqueta}</p>
              {grupo.modulos.map((modulo) => (
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
            </div>
          ))}
        </nav>
      </aside>

      <main className="contenido">{moduloActivo?.contenido}</main>
    </div>
  )
}
