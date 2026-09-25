using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Traductor;

public sealed class RectDto
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

/// <summary>
/// Canal local entre el Guest y el traductor (plan §3): un WebSocket
/// embebido con `HttpListener` de la propia .NET, sin librerías de terceros
/// que actualizar en cada gabinete. El traductor es el servidor; el Guest se
/// conecta como cliente solo cuando corre en el gabinete (`CabinetService`
/// del lado Angular).
///
/// El "apagado" de modo juego no se decide aquí a partir de si el socket se
/// cae: esa decisión es de <see cref="ModoJuegoState"/> (el vigilante de
/// latido), precisamente para no apagar de golpe ante una reconexión breve.
/// </summary>
public sealed class WebSocketBridge
{
    private readonly int _port;
    private readonly ModoJuegoState _state;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;

    /// Se dispara con el rect (en px CSS de pantalla) que el Guest reporta al
    /// montar la pantalla de juego, y con el modo de navegación que pide para
    /// esa sesión (`"cursor"` o `"mapeado"`). Ambos pueden ser null si el
    /// mensaje no los trae — un Guest anterior a esta versión no manda modo, y
    /// entonces vale el de siempre, cursor libre.
    public event Action<RectDto?, string?>? ModoJuegoOnRequested;

    /// El Guest pide que el cursor real salte a un punto (px CSS de pantalla).
    public event Action<double, double>? CursorRequested;

    public WebSocketBridge(int port, ModoJuegoState state)
    {
        _port = port;
        _state = state;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
        _listener.Start();
        _ = AcceptLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { /* ya pudo haberse cerrado */ }
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener!.GetContextAsync();
            }
            catch
            {
                if (token.IsCancellationRequested) return;
                continue;
            }

            if (ctx.Request.IsWebSocketRequest)
            {
                _ = HandleClientAsync(ctx, token);
            }
            else
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.Close();
            }
        }
    }

    private async Task HandleClientAsync(HttpListenerContext ctx, CancellationToken token)
    {
        WebSocketContext wsContext;
        try
        {
            wsContext = await ctx.AcceptWebSocketAsync(subProtocol: null);
        }
        catch
        {
            ctx.Response.StatusCode = 500;
            ctx.Response.Close();
            return;
        }

        var socket = wsContext.WebSocket;
        var buffer = new byte[4096];
        Log.Write("Guest conectado por WebSocket.");

        try
        {
            while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", token);
                    break;
                }

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                HandleMessage(json);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            Log.Write("Guest desconectado del WebSocket.");
        }
    }

    private void HandleMessage(string json)
    {
        InboundMessage? msg;
        try
        {
            msg = JsonSerializer.Deserialize<InboundMessage>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (JsonException ex)
        {
            Log.Write($"Mensaje no válido del Guest, ignorado: {ex.Message}");
            return;
        }

        if (msg?.Type is null) return;

        switch (msg.Type)
        {
            case "modo_juego":
                if (msg.On == true)
                {
                    _state.SetOn();
                    ModoJuegoOnRequested?.Invoke(msg.Rect, msg.Navegacion);
                }
                else
                {
                    _state.SetOff();
                }
                break;
            case "cursor_a":
                if (msg.X is double x && msg.Y is double y)
                {
                    CursorRequested?.Invoke(x, y);
                }
                break;
            case "ping":
                _state.Ping();
                break;
        }
    }

    // `internal` y no `private` para que `JsonContext` pueda nombrarla.
    internal sealed class InboundMessage
    {
        public string? Type { get; set; }
        public bool? On { get; set; }
        public RectDto? Rect { get; set; }

        /// Solo en `modo_juego`: `"cursor"` (por defecto) o `"mapeado"`.
        public string? Navegacion { get; set; }

        /// Solo en `cursor_a`: destino del salto, en px CSS de pantalla.
        public double? X { get; set; }
        public double? Y { get; set; }
    }
}
