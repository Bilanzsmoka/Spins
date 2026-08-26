import { useState } from 'react'

/**
 * El A/B/C no es motivacional: es un inventario. Sin saber qué separa tu
 * peor versión de tu mejor versión, no sabés qué estás corrigiendo. Va acá
 * adentro porque de nada sirve pedir la calificación si no se explica qué
 * significa cada letra.
 */
export function AyudaNivelDeJuego() {
  const [abierta, setAbierta] = useState(false)

  return (
    <div className="ayuda">
      <button
        type="button"
        className="ayuda-disparador"
        aria-expanded={abierta}
        onClick={() => setAbierta((previo) => !previo)}
      >
        {abierta ? 'Ocultar' : '¿Qué es A, B y C?'}
      </button>

      {abierta && (
        <div className="ayuda-cuerpo">
          <p>
            Tu habilidad no es un punto fijo: es un rango. El mismo día podés jugar
            muy bien y muy mal. Ponerle nombre a esos extremos es lo que permite
            trabajarlos por separado.
          </p>

          <dl className="ayuda-niveles">
            <div>
              <dt className="nivel nivel-a">A</dt>
              <dd>
                <strong>Tu mejor versión.</strong> Decidís con calma, seguís tu plan,
                usás las tablas sin dudar y aceptás los malos resultados sin que te
                muevan. Si te preguntaran por qué hiciste algo, tendrías la respuesta.
              </dd>
            </div>
            <div>
              <dt className="nivel nivel-b">B</dt>
              <dd>
                <strong>Tu versión normal.</strong> Jugás bien pero en piloto automático.
                Algunas decisiones las tomás por costumbre más que por análisis. No
                cometés errores graves, pero tampoco estás del todo presente.
              </dd>
            </div>
            <div>
              <dt className="nivel nivel-c">C</dt>
              <dd>
                <strong>Tu peor versión.</strong> Jugás rápido para recuperar, pagás
                por curiosidad, abrís más mesas de las que podés seguir, o seguís
                jugando cansado. Acá es donde se va el dinero que ganaste en A.
              </dd>
            </div>
          </dl>

          <p className="ayuda-remate">
            Progresás moviendo <strong>los dos extremos</strong>: estudiando subís el
            techo, pero <strong>subir el piso rinde más</strong>. Jugar menos veces en
            C pesa más que jugar mejor en A — por eso vale la pena anotarlo todos los
            días, aunque duela.
          </p>
        </div>
      )}
    </div>
  )
}
