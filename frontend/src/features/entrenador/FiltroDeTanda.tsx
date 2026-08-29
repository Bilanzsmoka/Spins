import type { SituacionResumen, TandaPedida } from '../../core/models/catalogo.model'

interface Props {
  situaciones: SituacionResumen[]
  pedida: TandaPedida
  onCambiar: (pedida: TandaPedida) => void
  onArrancar: () => void
  cargando: boolean
}

/** Los tamaños de tanda que se ofrecen. El del spec, 20, va primero. */
const TAMANOS = [20, 10, 40, 60]

/**
 * Sobre qué entrenar. Todo sale del catálogo: los formatos son los que los
 * archivos declaran y las situaciones se acotan al formato elegido, igual que
 * en los selectores de la grilla.
 *
 * El rango de stack va en BB y se compara contra la cobertura real de cada
 * tabla, no contra su clave: pedir de 7 a 12 trae toda tabla cuya banda toque
 * ese tramo.
 */
export function FiltroDeTanda({
  situaciones, pedida, onCambiar, onArrancar, cargando,
}: Props) {
  const formatos = [...new Set(situaciones.map((s) => s.formato))]
  const delFormato = pedida.formato
    ? situaciones.filter((s) => s.formato === pedida.formato)
    : situaciones

  return (
    <div className="filtro-tanda">
      <label>
        Formato
        <select
          value={pedida.formato ?? ''}
          onChange={(e) =>
            onCambiar({ ...pedida, formato: e.target.value || null, situacion: null })}
        >
          <option value="">Todos</option>
          {formatos.map((f) => <option key={f} value={f}>{f}</option>)}
        </select>
      </label>

      <label>
        Situación
        <select
          value={pedida.situacion ?? ''}
          onChange={(e) => onCambiar({ ...pedida, situacion: e.target.value || null })}
        >
          <option value="">Todas</option>
          {delFormato.map((s) => (
            <option key={s.clave} value={s.clave}>{s.etiqueta}</option>
          ))}
        </select>
      </label>

      <label>
        Stack desde
        <input
          type="number" min={1} max={200} inputMode="numeric"
          value={pedida.minBB ?? ''}
          onChange={(e) =>
            onCambiar({ ...pedida, minBB: e.target.value ? Number(e.target.value) : null })}
        />
      </label>

      <label>
        hasta
        <input
          type="number" min={1} max={200} inputMode="numeric"
          value={pedida.maxBB ?? ''}
          onChange={(e) =>
            onCambiar({ ...pedida, maxBB: e.target.value ? Number(e.target.value) : null })}
        />
      </label>

      <label>
        Manos
        <select
          value={pedida.tamano}
          onChange={(e) => onCambiar({ ...pedida, tamano: Number(e.target.value) })}
        >
          {TAMANOS.map((t) => <option key={t} value={t}>{t}</option>)}
        </select>
      </label>

      <button
        type="button" className="boton-principal"
        disabled={cargando} onClick={onArrancar}
      >
        {cargando ? 'Armando…' : 'Arrancar'}
      </button>
    </div>
  )
}
