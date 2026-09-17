namespace Traductor;

/// <summary>
/// Log mínimo a archivo (`traductor.log`, junto al `.exe`). El traductor no
/// tiene ventana ni consola (#414 CA1), así que sin esto no habría forma de
/// saber si el hook se instaló, si el Guest se conectó, o por qué no.
/// </summary>
internal static class Log
{
    private static readonly string FilePath =
        Path.Combine(AppContext.BaseDirectory, "traductor.log");
    private static readonly object Lock = new();

    public static void Write(string message)
    {
        try
        {
            lock (Lock)
            {
                File.AppendAllText(
                    FilePath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Si ni el log se puede escribir, no hay nada más que hacer aquí
            // — no debe tumbar el traductor por esto.
        }
    }
}
