using System.Text;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

public class Utf8StreamDecoderTests
{
    [Theory]
    [InlineData("año ñandú")]
    [InlineData("┌──┐ │ └──┘")]
    [InlineData("日本語 🚀 emoji")]
    public void Decode_ByteAByte_ReconstruyeElTextoOriginal(string texto)
    {
        var bytes = Encoding.UTF8.GetBytes(texto);
        var decoder = new Utf8StreamDecoder();
        var sb = new StringBuilder();

        foreach (var b in bytes)
            sb.Append(decoder.Decode(new[] { b }));

        Assert.Equal(texto, sb.ToString());
        Assert.DoesNotContain('�', sb.ToString());
    }

    [Fact]
    public void Decode_CaracterPartidoEnDosChunks_NoEmiteReemplazo()
    {
        var bytes = Encoding.UTF8.GetBytes("é"); // 0xC3 0xA9
        var decoder = new Utf8StreamDecoder();

        Assert.Equal("", decoder.Decode(bytes.AsSpan(0, 1)));
        Assert.Equal("é", decoder.Decode(bytes.AsSpan(1, 1)));
    }

    [Fact]
    public void Reset_DescartaBytesPendientes()
    {
        var bytes = Encoding.UTF8.GetBytes("é");
        var decoder = new Utf8StreamDecoder();

        decoder.Decode(bytes.AsSpan(0, 1));
        decoder.Reset();

        Assert.Equal("A", decoder.Decode("A"u8));
    }
}
