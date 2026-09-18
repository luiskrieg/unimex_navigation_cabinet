using System.Text.Json;
using System.Text.Json.Serialization;

namespace Traductor;

/// <summary>
/// Mapas de (de)serialización generados en tiempo de compilación.
///
/// Existen por el recorte (`PublishTrimmed` en Traductor.csproj): el camino
/// normal de System.Text.Json descubre las propiedades por reflexión, y el
/// recortador —que no puede saber qué tipos se leen por reflexión— borra esas
/// propiedades del binario. El síntoma sería el peor posible: `config.json` se
/// lee "bien" pero todos los valores salen por defecto, sin excepción ni línea
/// de log. Con el generador, el mapa se escribe en compilación y el recortador
/// lo ve y lo conserva.
///
/// Hay que deserializar con la sobrecarga que recibe un `JsonTypeInfo`
/// (<c>Insensible.RawConfig</c>), no con la genérica: la genérica sigue estando
/// marcada como no apta para recorte aunque se le pase un resolutor.
/// </summary>
[JsonSerializable(typeof(AppConfig.RawConfig))]
[JsonSerializable(typeof(WebSocketBridge.InboundMessage))]
internal sealed partial class TraductorJson : JsonSerializerContext
{
    /// Instancia propia para conservar el `PropertyNameCaseInsensitive` que
    /// tenía el código antes: alguien puede editar `config.json` a mano y
    /// escribir `KeyMap` en vez de `keyMap`. Se comparte en vez de crear
    /// opciones nuevas en cada mensaje — el puente WebSocket deserializa un
    /// `ping` cada 500 ms.
    public static readonly TraductorJson Insensible = new(new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    });
}
