interface Props {
  /** El motor arrancó bien y hay algo que encender. */
  disponible: boolean
  /** El usuario lo tiene encendido ahora. */
  activo: boolean
  cambiando: boolean
  falla: string | null
  fallaAlHablar: string | null
  errorAlCambiar: string | null
  onAlternar: () => void
}

/**
 * El interruptor del copiloto. Se apaga entre sesiones para que la
 * aplicación no conteste sola mientras no se está jugando.
 */
export function ControlDeVoz({
  disponible, activo, cambiando, falla, fallaAlHablar, errorAlCambiar, onAlternar,
}: Props) {
  const estado = !disponible ? 'sin-motor' : activo ? 'activo' : 'apagado'
  const texto = { 'sin-motor': 'Sin micrófono', activo: 'Escuchando', apagado: 'Apagado' }[estado]

  return (
    <section className="control-voz" aria-live="polite">
      <div className="control-voz-cabecera">
        <span className={`punto punto-${estado}`} aria-hidden="true" />
        <span className="control-voz-estado">{texto}</span>
      </div>

      <button
        type="button"
        className={`boton-voz${activo ? ' boton-voz-activo' : ''}`}
        disabled={!disponible || cambiando}
        onClick={onAlternar}
      >
        {cambiando ? '…' : activo ? 'Apagar voz' : 'Encender voz'}
      </button>

      {/* El reconocedor no arrancó: distinto de una respuesta muda puntual. */}
      {falla && <p className="aviso-voz">No se pudo iniciar el reconocedor: {falla}</p>}
      {/* La síntesis falló en la última respuesta, pero se sigue escuchando. */}
      {fallaAlHablar && <p className="aviso-voz">No se pudo hablar la última respuesta.</p>}
      {errorAlCambiar && <p className="aviso-voz">{errorAlCambiar}</p>}
    </section>
  )
}
