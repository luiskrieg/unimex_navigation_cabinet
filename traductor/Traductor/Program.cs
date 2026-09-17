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

        var hook = new KeyboardHook { ShouldSwallow = controller.ShouldSwallow };
        hook.KeyDown += controller.OnKeyDown;
        hook.KeyUp += controller.OnKeyUp;
        hook.Install();

        var bridge = new WebSocketBridge(config.WebSocketPort, state);
        bridge.ModoJuegoOnRequested += controller.EnterGameMode;

        // Única fuente de verdad para "salir de modo juego" (ver
        // GameModeController.ExitGameMode): cubre tanto el aviso explícito
        // del Guest como el vencimiento del vigilante de latido.
        state.TurnedOff += controller.ExitGameMode;

        bridge.Start();

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
