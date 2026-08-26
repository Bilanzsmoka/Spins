namespace PokerProOS.Application.Tablas;

public record ProblemaDeTabla(string Archivo, string Stack, string Spot, string Mensaje);

public record ResultadoDeValidacion(IReadOnlyList<ProblemaDeTabla> Problemas)
{
    public bool EsValido => Problemas.Count == 0;
}
