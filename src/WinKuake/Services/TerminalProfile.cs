namespace WinKuake.Services;

/// <summary>Perfil de terminal: cómo se muestra y cómo se lanza.</summary>
public sealed record TerminalProfile(string DisplayName, string WtArgs)
{
    /// <summary>GUID del perfil tal y como aparece en settings.json de Windows Terminal. Null para perfiles sintéticos.</summary>
    public string? Guid { get; init; }

    /// <summary>Path absoluto al ícono si se pudo resolver desde settings.json. Null si es ms-appx:// u otro no resoluble.</summary>
    public string? IconPath { get; init; }

    /// <summary>True si es el perfil marcado como predeterminado en wt.</summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// Línea de comandos lista para pasar a CreateProcess (ConPTY) — incluye
    /// argumentos. Ej.: <c>"C:\Program Files\Git\bin\bash.exe" -l -i</c>,
    /// <c>wsl.exe -d Ubuntu</c>, <c>pwsh.exe</c>. Null = no soportable (ej.
    /// Azure Cloud Shell, que requiere auth Azure).
    /// </summary>
    public string? CommandLine { get; init; }

    /// <summary>Directorio inicial. Null = heredar del proceso padre.</summary>
    public string? StartingDirectory { get; init; }

    public override string ToString() => DisplayName;
}
