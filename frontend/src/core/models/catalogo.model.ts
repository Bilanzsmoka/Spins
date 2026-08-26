export interface AccionDefinida {
  clave: string
  etiqueta: string
  color: string
  colorTexto: string
  orden: number
  dichos: string[]
}

export interface SpotResumen {
  clave: string
  etiqueta: string
}

export interface StackResumen {
  clave: string
  minBB: number
  maxBB: number
  spots: SpotResumen[]
}

export interface SituacionResumen {
  clave: string
  etiqueta: string
  stacks: StackResumen[]
}

export interface ProblemaDeTabla {
  archivo: string
  stack: string
  spot: string
  mensaje: string
}

export interface Catalogo {
  acciones: AccionDefinida[]
  situaciones: SituacionResumen[]
  problemas: ProblemaDeTabla[]
}

export interface Celda {
  mano: string
  accion: string
}

export interface SpotCompleto {
  clave: string
  etiqueta: string
  celdas: Celda[]
  conteos: Record<string, number>
}

export interface EventoDeVoz {
  textoCrudo: string
  manoInterpretada: string
  respuesta: string
  accion: string
  resuelta: boolean
  situacion: string | null
  claveDeStack: string | null
  spot: string | null
}

export interface EstadoDeVoz {
  /** El motor arrancó bien. No dice si está encendido ahora. */
  escuchando: boolean
  /** El usuario lo tiene encendido en este momento. */
  activo: boolean
  falla: string | null
  fallaAlHablar: string | null
  ultimaFrase: string | null
}

/** Un evento de voz con la hora en que llegó, para el historial escrito. */
export interface ConsultaRegistrada extends EventoDeVoz {
  hora: string
}

/* ---------- Diario ---------- */

export interface HabitoDefinido {
  clave: string
  etiqueta: string
  tipo: 'binario' | 'numero'
  orden: number
  ayuda: string
  invertido: boolean
}

/** Lo que te propusiste el día anterior y cómo salió. */
export interface Comparativa {
  fechaPrevia: string | null
  objetivoPrevio: string | null
  cumplimientoPrevio: number | null
  nivelPrevio: string | null
  volumenPrevio: number | null
  volumenDeHoy: number | null
  consultasPrevias: number
  consultasDeHoy: number
}

export interface EntradaDeDiario {
  id: number
  fecha: string
  intencion: string | null
  nivelDeJuego: string | null
  disparador: string | null
  mesas: number | null
  minutos: number | null
  notas: string
  objetivoTecnico: string | null
  cumplimientoObjetivo: number | null
  creadaEn: string
  actualizadaEn: string
}

export interface ManoConsultada {
  mano: string
  accion: string
  veces: number
}

/** Lo que ningún tracker tiene: qué preguntaste ese día, o sea qué no sabías. */
export interface ResumenDelDia {
  consultas: number
  resueltas: number
  manosMasConsultadas: ManoConsultada[]
  primeraHora: string | null
  ultimaHora: string | null
}

export interface DiaDeDiario {
  entrada: EntradaDeDiario | null
  resumen: ResumenDelDia
  marcas: Record<string, number>
  comparativa: Comparativa
}

export interface EntradaEnviada {
  intencion: string | null
  nivelDeJuego: string | null
  disparador: string | null
  mesas: number | null
  minutos: number | null
  notas: string
  objetivoTecnico: string | null
  cumplimientoObjetivo: number | null
  habitos: Record<string, number>
}
