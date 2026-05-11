using System;

namespace WinKuake.Services;

/// <summary>
/// Cálculo puro de ratios pantalla/ventana. Extraído de MainWindow para
/// poder testearlo sin un WPF Window vivo.
/// </summary>
public static class GeometryRatios
{
    /// <summary>
    /// Convierte el tamaño actual de la ventana a fracciones (0..1) de la
    /// pantalla. Clamp defensivo entre 0.1 y 1.0 para evitar persistir
    /// ratios degenerados (p.ej. ventana minimizada con Height=0).
    /// </summary>
    public static (double widthRatio, double heightRatio) FromSize(
        double windowWidth, double windowHeight, double screenWidth, double screenHeight)
    {
        if (screenWidth  <= 0) screenWidth  = 1;
        if (screenHeight <= 0) screenHeight = 1;
        var w = Math.Clamp(windowWidth  / screenWidth,  0.1, 1.0);
        var h = Math.Clamp(windowHeight / screenHeight, 0.1, 1.0);
        return (w, h);
    }

    /// <summary>
    /// True si la diferencia entre el ratio actual y el persistido amerita
    /// re-guardar a disco. Filtra ruido de pixel single (resize de 1px no
    /// debería disparar IO).
    /// </summary>
    public static bool RatiosDifferEnoughToSave(
        double oldRatio, double newRatio, double epsilon = 0.005)
    {
        return Math.Abs(oldRatio - newRatio) > epsilon;
    }
}
