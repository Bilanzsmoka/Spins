interface Props {
  escuchando: boolean
  falla: string | null
  fallaAlHablar: string | null
  ultimaFrase: string | null
  manoInterpretada: string | null
  respuesta: string | null
  colorRespuesta: string | null
}

export function EstadoDeVoz({
  escuchando, falla, fallaAlHablar, ultimaFrase, manoInterpretada, respuesta, colorRespuesta,
}: Props) {
  return (
    <section className="estado-voz" aria-live="polite">
      <span className={`indicador${escuchando ? ' indicador-activo' : ''}`}>
        {escuchando ? 'Escuchando' : 'Sin voz'}
      </span>
      {/* El reconocedor no arranco: distinto de una respuesta muda puntual. */}
      {falla && <span className="aviso-voz">No se pudo iniciar el reconocedor: {falla}</span>}
      {/* La sintesis fallo en la ultima respuesta, pero se sigue escuchando. */}
      {fallaAlHablar && <span className="aviso-voz">No se pudo hablar la última respuesta.</span>}
      {/* Ver lo que escucho es lo que permite detectar que entendio mal. */}
      {ultimaFrase && <span className="frase-cruda">«{ultimaFrase}»</span>}
      {manoInterpretada && <strong className="mano-interpretada">{manoInterpretada}</strong>}
      {respuesta && (
        <span className="respuesta" style={colorRespuesta ? { color: colorRespuesta } : undefined}>
          {respuesta}
        </span>
      )}
    </section>
  )
}
