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
  escuchando: boolean
  falla: string | null
  fallaAlHablar: string | null
  ultimaFrase: string | null
}
