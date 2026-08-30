import type { SituacionResumen, TandaPedida } from '../../core/models/catalogo.model'

interface Props {
  situaciones: SituacionResumen[]
  pedida: TandaPedida
  onCambiar: (pedida: TandaPedida) => void
  onArrancar: () => void
  cargando: boolean
}

/** Los tamaños de tanda que se ofrecen. El del spec, 20, va primero. */
/*
 * Diez primero: es el que conviene. Cinco existe para las ráfagas de dos
 * minutos —las que se hacen igual estando cansado— y los grandes quedan para
 * cuando querés sentarte a hacer volumen.
 */
const TAMANOS = [10, 5, 20, 40]

/**
 * Sobre qué entrenar. Todo sale del catálogo: los formatos son los que los
 * archivos declaran, las situaciones se acotan al formato elegido y los spots
 * a la situación, igual que en los selectores de la grilla.
 *
 * Cada nivel resetea al de abajo: un spot de otra tabla no significa nada, y
 * dejarlo puesto arma una tanda vacía sin decir por qué.
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
  const deLaSituacion = pedida.situacion
    ? delFormato.filter((s) => s.clave === pedida.situacion)
    : delFormato

  // Un mismo spot aparece en muchos stacks con la misma clave: sin deduplicar,
  // la lista repetiría la misma opción decenas de veces.
  const spots = [...new Map(
    deLaSituacion
      .flatMap((s) => s.stacks.flatMap((t) => t.spots))
      .map((p) => [p.clave, p.etiqueta] as const),
  )]

  return (
    <div className="filtro-tanda">
      <label>
        Formato
        <select
          value={pedida.formato ?? ''}
          onChange={(e) =>
            onCambiar({
              ...pedida, formato: e.target.value || null, situacion: null, spot: null,
            })}
        >
          <option value="">Todos</option>
          {formatos.map((f) => <option key={f} value={f}>{f}</option>)}
        </select>
      </label>

      <label>
        Situación
        <select
          value={pedida.situacion ?? ''}
          onChange={(e) =>
            onCambiar({ ...pedida, situacion: e.target.value || null, spot: null })}
        >
          <option value="">Todas</option>
          {delFormato.map((s) => (
            <option key={s.clave} value={s.clave}>{s.etiqueta}</option>
          ))}
        </select>
      </label>

      <label>
        Spot
        <select
          value={pedida.spot ?? ''}
          onChange={(e) => onCambiar({ ...pedida, spot: e.target.value || null })}
        >
          <option value="">Todos</option>
          {spots.map(([clave, etiqueta]) => (
            <option key={clave} value={clave}>{etiqueta}</option>
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
