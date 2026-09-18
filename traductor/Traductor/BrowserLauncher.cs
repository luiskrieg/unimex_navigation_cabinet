using System.Diagnostics;
using Microsoft.Win32;

namespace Traductor;

/// <summary>
/// Abre el Guest al arrancar, para que el gabinete quede listo con un solo
/// doble clic (o una sola Tarea Programada) en vez de dos piezas sueltas.
///
/// Es opcional a propósito: sin `guestUrl` en `config.json` el traductor se
/// comporta exactamente como antes —solo escucha en `127.0.0.1`— y el
/// navegador lo abre quien quiera, que es el reparto que describe
/// `kiosko/GUIA-INSTALACION.md` para el gabinete definitivo.
/// </summary>
internal static class BrowserLauncher
{
    /// <summary>
    /// Devuelve el proceso del navegador cuando se pudo lanzar Chrome de
    /// forma vigilable, o `null` si se cayó al navegador por defecto del
    /// sistema (ahí el proceso que arranca no es el que se queda abierto,
    /// así que no sirve para vigilar).
    /// </summary>
    public static Process? Launch(string url, bool kiosk)
    {
        var chrome = FindChrome();

        if (chrome is null)
        {
            Log.Write("No se encontró Chrome; se abre el navegador por defecto (sin kiosko).");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return null;
        }

        // Perfil propio en los dos modos, no solo en kiosko. Sin esto, si la
        // persona ya tiene Chrome abierto, Windows le entrega la URL a esa
        // instancia y el proceso que arrancamos muere en el acto: `--kiosk`
        // se ignoraría en silencio y, peor, el traductor creería que el
        // navegador se cerró y se apagaría solo al instante.
        var profile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Traductor",
            "chrome-perfil");
        Directory.CreateDirectory(profile);

        var args = new List<string>
        {
            $"--user-data-dir=\"{profile}\"",
            "--no-first-run",
            "--no-default-browser-check",
            "--noerrdialogs",
            "--disable-session-crashed-bubble",
        };
        if (kiosk) args.Insert(0, "--kiosk");
        args.Add($"\"{url}\"");

        return Process.Start(new ProcessStartInfo(chrome, string.Join(" ", args))
        {
            UseShellExecute = false,
        });
    }

    private static string? FindChrome()
    {
        // Lo que Windows mismo usa para resolver "chrome.exe" a secas.
        const string appPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe";
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var key = root.OpenSubKey(appPath);
            if (key?.GetValue(null) is string path && File.Exists(path)) return path;
        }

        // Respaldo por si el registro no lo tiene (instalaciones portables).
        var candidates = new[]
        {
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Google\Chrome\Application\chrome.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
