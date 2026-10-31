using System.Text;

namespace WinKuake.Services;

/// <summary>
/// Decodifica un stream UTF-8 por chunks conservando los bytes incompletos
/// entre llamadas: ConPTY puede partir un carácter multibyte entre dos lecturas.
/// No es thread-safe; usar desde un solo hilo (el de lectura del PTY).
/// </summary>
public sealed class Utf8StreamDecoder
{
    private readonly Decoder _decoder = new UTF8Encoding(false).GetDecoder();

    public string Decode(ReadOnlySpan<byte> chunk)
    {
        var chars = new char[_decoder.GetCharCount(chunk, flush: false)];
        var n = _decoder.GetChars(chunk, chars, flush: false);
        return new string(chars, 0, n);
    }

    /// <summary>Descarta bytes pendientes; usar al reiniciar el shell para no arrastrar un carácter a medias.</summary>
    public void Reset() => _decoder.Reset();
}
