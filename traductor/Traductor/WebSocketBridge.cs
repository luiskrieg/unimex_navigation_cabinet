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

    /// Se dispara con el rect (en px CSS de viewport) que el Guest reporta
    /// al montar la pantalla de juego. Puede ser null si el mensaje no lo trae.
    public event Action<RectDto?>? ModoJuegoOnRequested;

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
        catch (JsonException)
        {
            return;
        }

        if (msg?.Type is null) return;

        switch (msg.Type)
        {
            case "modo_juego":
                if (msg.On == true)
                {
                    _state.SetOn();
                    ModoJuegoOnRequested?.Invoke(msg.Rect);
                }
                else
                {
                    _state.SetOff();
                }
                break;
            case "ping":
                _state.Ping();
                break;
        }
    }

    private sealed class InboundMessage
    {
        public string? Type { get; set; }
        public bool? On { get; set; }
        public RectDto? Rect { get; set; }
    }
}
