using System.Text;

namespace Traductor;

/// <summary>
/// Log mínimo a archivo (`traductor.log`, junto al `.exe`). El traductor no
/// tiene ventana ni consola (#414 CA1), así que sin esto no habría forma de
/// saber si el hook se instaló, si el Guest se conectó, o por qué no.
///
/// El tamaño está acotado a propósito: un gabinete corre meses sin que nadie
/// lo mire, y un ciclo de reconexión del Guest puede escribir dos líneas por
/// segundo. Al pasar de `logMaxBytes` el archivo se recicla a
/// `traductor.log.old` y se empieza uno nuevo, así que el traductor nunca
/// ocupa en disco más de dos veces ese tope, corra el tiempo que corra.
/// </summary>
internal static class Log
{
    private static readonly string FilePath =
        Path.Combine(AppContext.BaseDirectory, "traductor.log");
    private static readonly string PreviousFilePath = FilePath + ".old";
    private static readonly object Lock = new();

    // Con BOM a propósito: sin él, el Bloc de notas y el `Get-Content` de
    // PowerShell leen el archivo como ANSI y los acentos salen rotos
    // ("se cerró" → "se cerrÃ³") justo cuando alguien está diagnosticando.
    private static readonly Encoding FileEncoding =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private static bool _enabled = true;
    private static long _maxBytes = 1024 * 1024;

    /// Se llama una sola vez al arrancar, ya con `config.json` leído y antes
    /// de la primera línea escrita.
    public static void Configure(bool enabled, long maxBytes)
    {
        lock (Lock)
        {
            _enabled = enabled;
            _maxBytes = maxBytes;
        }
    }

    public static void Write(string message)
    {
        try
        {
            lock (Lock)
            {
                if (!_enabled) return;
                RecycleIfFull();
                File.AppendAllText(
                    FilePath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}",
                    FileEncoding);
            }
        }
        catch
        {
            // Si ni el log se puede escribir, no hay nada más que hacer aquí
            // — no debe tumbar el traductor por esto.
        }
    }

    /// Un tope de 0 o menos desactiva el reciclado (log sin límite).
    private static void RecycleIfFull()
    {
        if (_maxBytes <= 0) return;

        var current = new FileInfo(FilePath);
        if (!current.Exists || current.Length < _maxBytes) return;

        // Se conserva una sola generación anterior: es lo que hace falta para
        // diagnosticar un gabinete sin que el disco crezca para siempre.
        File.Move(FilePath, PreviousFilePath, overwrite: true);
    }
}
