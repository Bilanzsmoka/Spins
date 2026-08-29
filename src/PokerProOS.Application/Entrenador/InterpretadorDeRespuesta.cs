using PokerProOS.Application.Tablas;
using PokerProOS.Application.Texto;

namespace PokerProOS.Application.Entrenador;

/// <summary>
/// El texto que oyó el navegador, entendido como una respuesta del
/// entrenamiento.
///
/// Es su propia pieza y no un modo de <c>InterpretadorDeTexto</c> a propósito:
/// no hace falta estado. La pantalla de entrenamiento manda su texto a su
/// endpoint, y quién sabe el modo es la pantalla, que ya lo sabe. Un flag
/// global de "estoy entrenando" es una variable más que puede quedar mal.
///
/// Las formas salen de los `dichos` de acciones.json, igual que todo lo demás
/// del proyecto: agregar una manera de decir "all in" no toca código.
/// </summary>
public sealed class InterpretadorDeRespuesta(IRegistroDeAcciones acciones)
{
    /// <summary>La clave de la acción dicha, o null si el texto no es una.</summary>
    public string? Interpretar(string texto)
    {
        var normalizado = NormalizadorDeTexto.EnFrase(texto);
        if (normalizado.Length == 0) return null;

        // La comparacion de abajo es por igualdad exacta, no por prefijo, asi
        // que un dicho corto nunca le gana a uno largo por si solo. El orden
        // no desempata eso -no hace falta-: existe para que el resultado sea
        // determinista si algun dia dos acciones llegan a compartir la misma
        // forma normalizada, en vez de depender de en que orden las declaro
        // acciones.json.
        var candidatas = acciones.Todas
            .SelectMany(a => a.Dichos.Select(d => (a.Clave, Dicho: NormalizadorDeTexto.EnFrase(d))))
            .Where(c => c.Dicho.Length > 0)
            .OrderByDescending(c => c.Dicho.Length);

        foreach (var (clave, dicho) in candidatas)
            if (normalizado == dicho) return clave;

        return null;
    }
}
