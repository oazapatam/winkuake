using System;
using System.Threading;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// xUnit corre tests en MTA por defecto. WPF requiere STA para tocar
/// Application.Current.Resources / Freezable. Usamos [Fact] normal pero
/// envolvemos el cuerpo del test en un thread STA mediante <see cref="OnStaThread"/>.
/// Es un patrón explícito (mejor que un atributo mágico).
/// </summary>
internal static class StaTestRunner
{
    /// <summary>Ejecuta <paramref name="test"/> en un thread STA y propaga la excepción si la hay.</summary>
    public static void OnStaThread(Action test)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { test(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured is not null) throw captured;
    }
}

// Alias semántico: las pruebas pueden usar [Fact] y llamar StaTestRunner.OnStaThread(() => { ... }).
// Mantenemos también esta clase vacía para compatibilidad con código que importe el nombre.
internal sealed class WpfFactAttribute : FactAttribute { }
