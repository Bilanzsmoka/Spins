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

export interface RivalEnLaMesa {
  posicion: string
  /** El término del glosario del que salen su color y su figura. */
  tipo: string
  /** "limp", "min-raise", "all-in", "call", "fold", "por actuar". */
  hizo: string
}

export interface MesaDeSituacion {
  heroe: string
  ciegaChica: number
  ciegaGrande: number
  rivales: RivalEnLaMesa[]
}

export interface SituacionResumen {
  clave: string
  etiqueta: string
  /** El formato de mesa ("HU", "3-max"), declarado por el archivo de la tabla. */
  formato: string
  /** Cómo se ve la mesa cuando te toca decidir. Nula si el archivo no la declara. */
  mesa: MesaDeSituacion | null
  /**
   * Qué es esta situación, escrito a mano. Nula si el archivo no la declara:
   * ningún cálculo puede deducir qué significa "BB vs 3-way limp".
   */
  explicacion: string | null
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

export interface ParteDeMix {
  accion: string
  frecuencia: number
}

export interface Celda {
  mano: string
  /** La acción dominante. En un mix, la de mayor frecuencia. */
  accion: string
  /** Las partes cuando la tabla prescribe una estrategia mixta. Nulo si es pura. */
  mix: ParteDeMix[] | null
}

export interface SpotCompleto {
  clave: string
  etiqueta: string
  celdas: Celda[]
  conteos: Record<string, number>
}

export interface PesoDeAccion {
  accion: string
  combos: number
  porcentajeDeBaraja: number
}

/** El bloque de una familia que comparte acción, y la mano que lo rompe. */
export interface AnclaDeFamilia {
  familia: string
  tope: string
  fondo: string
  accion: string
  siguiente: string | null
  accionSiguiente: string | null
}

export interface BandaDeStack {
  claveDeStack: string
  minBB: number
  maxBB: number
  accion: string
  /** Si el stack consultado cae adentro de esta banda. */
  esElActual: boolean
}

export interface PasoDeLinea {
  spot: string
  etiqueta: string
  accion: string
  esElConsultado: boolean
}

export interface FichaDeMemoria {
  mano: string
  accion: string
  claveDeStack: string
  pesos: PesoDeAccion[]
  ancla: AnclaDeFamilia | null
  umbral: BandaDeStack[]
  familias: AnclaDeFamilia[]
  linea: PasoDeLinea[]
  tip: string | null
  /** El spot contado en pocas frases. Es lo que de verdad se memoriza. */
  reglas: ReglaDelSpot[]
}

/**
 * Las tres cosas que puede ser un dictado. 'resuelta' sola no alcanza: una
 * orden de contexto se entiende perfectamente y no resuelve ninguna mano.
 */
export type TipoDeDictado = 'Mano' | 'Contexto' | 'Ignorado'

export interface EventoDeVoz {
  textoCrudo: string
  manoInterpretada: string
  respuesta: string
  accion: string
  resuelta: boolean
  tipo: TipoDeDictado
  situacion: string | null
  claveDeStack: string | null
  spot: string | null
  ficha: FichaDeMemoria | null
  /**
   * El palo no se dictó y se asumió offsuit. Se muestra porque en silencio
   * es una trampa: si el reconocedor se come el «suited», la respuesta es de
   * otra casilla y en pantalla no se nota.
   */
  paloAsumido: boolean
}

/**
 * Lo único que el servidor sabe de la voz: la última frase que le llegó.
 * Escuchar, hablar y estar encendido son del navegador, que no necesita
 * preguntárselo a nadie.
 */
export interface EstadoDeVoz {
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

export interface DiaDeGrilla {
  fecha: string
  nivelDeJuego: string | null
  marcas: Record<string, number>
  notas: Record<string, string>
}

export interface ResumenDeHabito {
  clave: string
  cumplidos: number
  diasRegistrados: number
  rachaActual: number
  mejorRacha: number
}

/**
 * Cómo jugaste los días que hiciste el hábito contra los que no.
 * `confiable` es falso cuando hay tan pocos días de un lado que el número
 * no significa nada.
 */
export interface CruceDeHabito {
  clave: string
  diasCon: number
  buenosCon: number
  diasSin: number
  buenosSin: number
  confiable: boolean
}

export interface ProgresoDeHabitos {
  desde: string
  hasta: string
  dias: DiaDeGrilla[]
  resumen: ResumenDeHabito[]
  cruces: CruceDeHabito[]
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
  notasDeHabitos: Record<string, string>
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
  notasDeHabitos: Record<string, string>
}

/* ---------- Vocabulario de voz ---------- */

export interface FormasHabladas {
  clave: string
  dichos: string[]
}

export interface Vocabulario {
  palabrasDeStack: string[]
  formatos: FormasHabladas[]
  rangos: FormasHabladas[]
  palos: FormasHabladas[]
  spots: FormasHabladas[]
  situaciones: FormasHabladas[]
  /** Manos enteras, con clave de la matriz ("AKo"). Arranca vacía. */
  manos: FormasHabladas[]
  /** Cómo se nombra cada nivel del flujo al encabezar un dictado dirigido. */
  niveles: FormasHabladas[]
}

export type CategoriaDeVocabulario =
  | 'Rangos' | 'Palos' | 'Spots' | 'Situaciones' | 'PalabrasDeStack' | 'Formatos'
  | 'Manos' | 'Niveles'

export interface ReglaDelSpot {
  /** Cómo se llama el grupo: "los Ax offsuit", "los pares". */
  grupo: string
  accion: string
  /** La mano más baja que todavía hace `accion`. Nula si el grupo no se corta. */
  hasta: string | null
  despues: string | null
  manos: number
}

/* ---------- Glosario ---------- */

export interface TerminoDelGlosario {
  termino: string
  explicacion: string
  /**
   * De acá para abajo es la ficha de perfil, y sólo la traen los jugadores.
   * Una palabra suelta del diccionario no tiene color ni ícono: se lee, no se
   * reconoce de un vistazo.
   */
  eje?: string
  perfil?: string
  color?: string
  colorTexto?: string
  icono?: string
  rasgos?: string[]
}

export interface EjeDelGlosario {
  clave: string
  /** Qué significan los colores de este eje. Un color sin convención es una mancha. */
  nota: string
}

export interface GrupoDelGlosario {
  clave: string
  titulo: string
  terminos: TerminoDelGlosario[]
  /** Los costados por los que se separan sus términos, en el orden que van en pantalla. */
  ejes?: EjeDelGlosario[]
}

/* ---------- El plan ---------- */

export interface EstadoDeHito {
  clave: string
  titulo: string
  tipo: string
  /** El número crudo, que es lo que impide leer el porcentaje como algo que no es. */
  hecho: number
  total: number
  porcentaje: number
  objetivo: number
  cumplido: boolean
  esElActivo: boolean
  situacion: string | null
  /** Por qué no se pudo medir. Un hito roto se muestra con su causa. */
  problema: string | null
}

export interface DiaDelPlan {
  fecha: string
  volumen: number
  alcanzo: boolean
  esHoy: boolean
}

export interface EstadoDelDia {
  metaDeVolumen: number
  volumenDeHoy: number
  estudioHecho: boolean
  hitos: EstadoDeHito[]
  semana: DiaDelPlan[]
  sinDosSeguidos: boolean
  situacionQueToca: string | null
}

export interface RespuestaDelPlan {
  hayPlan: boolean
  estado?: EstadoDelDia
}

/* ---------- Entrenador ---------- */

export interface PreguntaDeTanda {
  situacion: string
  etiquetaDeSituacion: string
  claveDeStack: string
  spot: string
  etiquetaDeSpot: string
  mano: string
  /** Material nuevo, sin progreso previo. */
  esNueva: boolean
}

export interface TandaPedida {
  formato: string | null
  situacion: string | null
  minBB: number | null
  maxBB: number | null
  spot: string | null
  tamano: number
}

export interface RespuestaEnviada {
  situacion: string
  claveDeStack: string
  spot: string
  mano: string
  accion: string
  /** Cuánto tardaste desde que apareció la pregunta. */
  milisegundos: number
}

export interface VeredictoDeRespuesta {
  acerto: boolean
  accionCorrecta: string
  mix: ParteDeMix[] | null
  /** Solo al fallar: acertar sigue de largo. */
  ficha: FichaDeMemoria | null
  /** Cuándo vuelve a preguntarse esta casilla. */
  vence: string
}
