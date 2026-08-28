namespace PokerProOS.Infrastructure;

/// <summary>
/// Se lanza cuando <c>acciones.json</c> o <c>vocabulario.json</c> no se
/// pueden leer o parsear. A diferencia de una tabla de estrategia rota —
/// donde el catálogo puede seguir sirviendo las diez tablas restantes—, no
/// hay nada útil que servir sin el registro: colores, validación de tablas
/// y la interpretación de lo dictado dependen de él. El mensaje nombra el archivo y
/// conserva la razón exacta (incluida la posición del JSON inválido, que
/// <see cref="System.Text.Json.JsonException.Message"/> ya trae) para que
/// un usuario que edita ese archivo a mano y comete un error de sintaxis
/// sepa qué corregir en vez de leer un stack trace en bruto.
/// </summary>
public sealed class RegistroInvalidoException : Exception
{
    public string RutaArchivo { get; }

    public RegistroInvalidoException(string rutaArchivo, Exception causa)
        : base($"No se pudo cargar el registro '{rutaArchivo}': {causa.Message}", causa)
        => RutaArchivo = rutaArchivo;
}
