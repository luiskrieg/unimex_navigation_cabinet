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

        // Sin esto el proceso terminaría de inmediato y el hook de teclado
        // nunca recibiría un solo evento (ver nota en Traductor.csproj).
        System.Windows.Forms.Application.Run();
    }
}
