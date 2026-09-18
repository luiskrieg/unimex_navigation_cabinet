using System.Text.Json;

namespace Traductor;

// Nota: usamos el enum `Keys` propio de este proyecto (ver NativeMethods.cs),
// no System.Windows.Forms.Keys — evita cargar la referencia completa de
// WinForms en un programa que no tiene ninguna ventana.

/// <summary>
/// #414 CA10 — todo lo que el equipo puede necesitar ajustar sin recompilar:
/// puerto del canal con el Guest, mapa de teclas de la botonera y velocidad
/// del cursor. Se lee una sola vez al arrancar.
///
/// Los valores por defecto de esta clase son exactamente los mismos que trae
/// el `config.json` que se distribuye: el `.exe` suelto, sin ningún archivo
/// al lado, se comporta igual que el paquete completo salvo por `GuestUrl`,
/// que es lo único que no se puede adivinar.
/// </summary>
public sealed class AppConfig
{
    public int WebSocketPort { get; init; } = 8765;
    public KeyMapConfig KeyMap { get; init; } = new();
    public CursorConfig Cursor { get; init; } = new();
    public int WatchdogTimeoutMs { get; init; } = 2000;
    public int PingIntervalMs { get; init; } = 500;

    /// Factor para convertir el rect que reporta el Guest (px CSS de
    /// viewport) a píxeles físicos de pantalla para `SetCursorPos` al entrar
    /// en modo juego. 1.0 asume kiosko a pantalla completa sin escalado de
    /// Windows ni zoom de Chrome — es el riesgo R-Gab3 del plan, se ajusta
    /// aquí sin recompilar si el gabinete real tiene otro DPI.
    public double DpiScale { get; init; } = 1.0;

    /// Dirección del Guest que se abre al arrancar. Vacía = el traductor no
    /// abre nada y solo escucha, que es el reparto del gabinete definitivo
    /// (ahí Chrome lo lanza su propia Tarea Programada, ver
    /// `kiosko/GUIA-INSTALACION.md`).
    public string GuestUrl { get; init; } = "";

    /// Solo aplica si hay `GuestUrl`: pantalla completa sin salida (gabinete)
    /// o ventana normal (para probar en una máquina cualquiera).
    public bool Kiosk { get; init; } = true;

    /// El log es la única ventana a un traductor que no tiene ventana; el
    /// tope de tamaño es lo que evita que eso crezca sin control en una
    /// máquina que nadie supervisa. Ver <see cref="Log"/>.
    public bool LogEnabled { get; init; } = true;
    public long LogMaxBytes { get; init; } = 1024 * 1024;

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            return new AppConfig();
        }

        var json = File.ReadAllText(path);
        var raw = JsonSerializer.Deserialize<RawConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? new RawConfig();

        return new AppConfig
        {
            WebSocketPort = raw.WebSocketPort,
            WatchdogTimeoutMs = raw.WatchdogTimeoutMs,
            PingIntervalMs = raw.PingIntervalMs,
            DpiScale = raw.DpiScale,
            GuestUrl = raw.GuestUrl,
            Kiosk = raw.Kiosk,
            LogEnabled = raw.LogEnabled,
            LogMaxBytes = raw.LogMaxBytes,
            Cursor = raw.Cursor,
            KeyMap = KeyMapConfig.FromRaw(raw.KeyMap),
        };
    }

    // Forma cruda del JSON (keyMap llega como listas de nombres de tecla).
    private sealed class RawConfig
    {
        public int WebSocketPort { get; set; } = 8765;
        public int WatchdogTimeoutMs { get; set; } = 2000;
        public int PingIntervalMs { get; set; } = 500;
        public double DpiScale { get; set; } = 1.0;
        public string GuestUrl { get; set; } = "";
        public bool Kiosk { get; set; } = true;
        public bool LogEnabled { get; set; } = true;
        public long LogMaxBytes { get; set; } = 1024 * 1024;
        public CursorConfig Cursor { get; set; } = new();
        public RawKeyMap KeyMap { get; set; } = new();
    }

    internal sealed class RawKeyMap
    {
        public string[] Up { get; set; } = { "Up" };
        public string[] Down { get; set; } = { "Down" };
        public string[] Left { get; set; } = { "Left" };
        public string[] Right { get; set; } = { "Right" };
        public string[] Confirm { get; set; } = { "Return", "Space" };
        public string[] Escape { get; set; } = { "Escape" };
        public string[] Back { get; set; } = { "Back", "BrowserBack" };
    }

    public sealed class KeyMapConfig
    {
        public HashSet<Keys> Up { get; init; } = new();
        public HashSet<Keys> Down { get; init; } = new();
        public HashSet<Keys> Left { get; init; } = new();
        public HashSet<Keys> Right { get; init; } = new();
        public HashSet<Keys> Confirm { get; init; } = new();
        public HashSet<Keys> Escape { get; init; } = new();
        public HashSet<Keys> Back { get; init; } = new();

        internal static KeyMapConfig FromRaw(RawKeyMap raw) => new()
        {
            Up = Parse(raw.Up),
            Down = Parse(raw.Down),
            Left = Parse(raw.Left),
            Right = Parse(raw.Right),
            Confirm = Parse(raw.Confirm),
            Escape = Parse(raw.Escape),
            Back = Parse(raw.Back),
        };

        private static HashSet<Keys> Parse(IEnumerable<string> names)
        {
            var set = new HashSet<Keys>();
            foreach (var name in names)
            {
                if (Enum.TryParse<Keys>(name, ignoreCase: true, out var key))
                {
                    set.Add(key);
                }
            }
            return set;
        }
    }
}

/// Paso del cursor en píxeles: arranca en <see cref="StepBase"/> y crece
/// <see cref="StepGrowth"/> por repetición mientras se mantenga la tecla,
/// hasta <see cref="AccelMax"/> repeticiones. El paso máximo es
/// StepBase + StepGrowth * AccelMax.
public sealed class CursorConfig
{
    public int StepBase { get; init; } = 10;
    public int StepGrowth { get; init; } = 3;
    public int AccelMax { get; init; } = 8;
}
