using System.Globalization;

namespace PokerProOS.Application.Voz;

/// <summary>
/// Convierte el número de un stack dictado a entero. Chrome devuelve a veces
/// dígitos ("9 bb") y a veces letras ("nueve be be") para la misma frase, así
/// que el intérprete tiene que entender las dos.
///
/// El rango útil es 1..99: es el que cubren las tablas, y acotarlo evita que
/// un año o un precio sueltos en una conversación pasen por stack.
/// </summary>
public static class NumeroHablado
{
    private static readonly Dictionary<string, int> Unidades = new()
    {
        ["uno"] = 1, ["dos"] = 2, ["tres"] = 3, ["cuatro"] = 4, ["cinco"] = 5,
        ["seis"] = 6, ["siete"] = 7, ["ocho"] = 8, ["nueve"] = 9, ["diez"] = 10,
        ["once"] = 11, ["doce"] = 12, ["trece"] = 13, ["catorce"] = 14, ["quince"] = 15,
        ["dieciseis"] = 16, ["diecisiete"] = 17, ["dieciocho"] = 18, ["diecinueve"] = 19,
        ["veinte"] = 20, ["veintiuno"] = 21, ["veintidos"] = 22, ["veintitres"] = 23,
        ["veinticuatro"] = 24, ["veinticinco"] = 25, ["veintiseis"] = 26,
        ["veintisiete"] = 27, ["veintiocho"] = 28, ["veintinueve"] = 29,
    };

    private static readonly Dictionary<string, int> Decenas = new()
    {
        ["treinta"] = 30, ["cuarenta"] = 40, ["cincuenta"] = 50, ["sesenta"] = 60,
        ["setenta"] = 70, ["ochenta"] = 80, ["noventa"] = 90,
    };

    public static int? Interpretar(string texto)
    {
        var limpio = (texto ?? "").Trim().ToLowerInvariant();
        if (limpio.Length == 0) return null;

        if (int.TryParse(limpio, NumberStyles.Integer, CultureInfo.InvariantCulture, out var digitos))
            return digitos is >= 1 and <= 99 ? digitos : null;

        if (Unidades.TryGetValue(limpio, out var unidad)) return unidad;
        if (Decenas.TryGetValue(limpio, out var decena)) return decena;

        // "treinta y cinco": la única forma compuesta del rango 30..99.
        var partes = limpio.Split(" y ", StringSplitOptions.TrimEntries);
        if (partes.Length == 2
            && Decenas.TryGetValue(partes[0], out var alta)
            && Unidades.TryGetValue(partes[1], out var baja)
            && baja <= 9)
            return alta + baja;

        return null;
    }
}
