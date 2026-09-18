using System.Diagnostics;

namespace Traductor;

/// <summary>
/// #414 CA1 — arranque sin ventana, ícono ni aviso visible. Este es el
/// único lugar del proyecto que arma las piezas entre sí; cada clase por
/// separado no sabe de las demás más de lo necesario.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var baseDir = AppContext.BaseDirectory;
        var config = AppConfig.Load(Path.Combine(baseDir, "config.json"));
        Log.Configure(config.LogEnabled, config.LogMaxBytes);

        var mouse = new MouseSimulator(config.Cursor);
        var cursor = new CursorAppearance(Path.Combine(baseDir, "cursor-gabinete.cur"));
        var state = new ModoJuegoState(config.WatchdogTimeoutMs);
        var controller = new GameModeController(config.KeyMap, mouse, cursor, state, config.DpiScale);

        Log.Write($"Traductor arrancando. Puerto WS: {config.WebSocketPort}. DpiScale: {config.DpiScale}.");

        var hook = new KeyboardHook { ShouldSwallow = controller.ShouldSwallow };
        hook.KeyDown += controller.OnKeyDown;
        hook.KeyUp += controller.OnKeyUp;
        try
        {
            hook.Install();
            Log.Write("Hook de teclado instalado correctamente.");
        }
        catch (Exception ex)
        {
            // No se aborta el proceso: al menos el canal WS queda arriba
            // para poder confirmar R-Gab1 aunque R-Gab2 (el hook) haya
            // fallado por antivirus/permisos (R-Gab4).
            Log.Write($"ERROR instalando el hook de teclado: {ex.Message}");
        }

        var bridge = new WebSocketBridge(config.WebSocketPort, state);
        bridge.ModoJuegoOnRequested += rect =>
        {
            Log.Write($"modo_juego: ON (rect={(rect is null ? "null" : $"{rect.X},{rect.Y},{rect.Width}x{rect.Height}")})");
            controller.EnterGameMode(rect);
        };

        // Única fuente de verdad para "salir de modo juego" (ver
        // GameModeController.ExitGameMode): cubre tanto el aviso explícito
        // del Guest como el vencimiento del vigilante de latido.
        state.TurnedOff += () =>
        {
            Log.Write("modo_juego: OFF");
            controller.ExitGameMode();
        };

        try
        {
            bridge.Start();
            Log.Write($"Servidor WebSocket escuchando en ws://127.0.0.1:{config.WebSocketPort}/");
        }
        catch (Exception ex)
        {
            Log.Write($"ERROR arrancando el servidor WebSocket: {ex.Message}");
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            hook.Dispose();
            bridge.Stop();
            state.Dispose();
            cursor.Restore();
        };

        // Después de `bridge.Start()` a propósito: si el Guest cargara antes
        // de que el puerto esté escuchando, su primer intento de conexión
        // fallaría y el gabinete arrancaría en modo navegador normal.
        OpenGuestIfConfigured(config);

        // Sin esto el proceso terminaría de inmediato y el hook de teclado
        // nunca recibiría un solo evento (ver nota en Traductor.csproj).
        System.Windows.Forms.Application.Run();
    }

    private static void OpenGuestIfConfigured(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.GuestUrl)) return;

        Process? browser;
        try
        {
            browser = BrowserLauncher.Launch(config.GuestUrl, config.Kiosk);
            Log.Write($"Guest abierto en {config.GuestUrl} (kiosko: {config.Kiosk}).");
        }
        catch (Exception ex)
        {
            // El traductor sigue en pie: alguien puede abrir el Guest a mano
            // y el canal WS lo va a recibir igual.
            Log.Write($"ERROR abriendo el Guest: {ex.Message}");
            return;
        }

        if (browser is null) return;

        // Si este proceso abrió el navegador, también se va con él: si no, el
        // traductor quedaría corriendo invisible y sin nada que traducir, y
        // solo se podría cerrar desde el Administrador de tareas.
        browser.EnableRaisingEvents = true;
        browser.Exited += (_, _) =>
        {
            Log.Write("El navegador se cerró; el traductor termina también.");
            // Environment.Exit —y no Application.Exit— porque esto llega en un
            // hilo del pool: dispara igual el ProcessExit que restaura el
            // cursor y suelta el hook.
            Environment.Exit(0);
        };
    }
}
