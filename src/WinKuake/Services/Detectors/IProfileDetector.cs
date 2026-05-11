using System.Collections.Generic;
using WinKuake.Models;

namespace WinKuake.Services.Detectors;

/// <summary>
/// Contrato de un detector de perfiles. Cada implementación mira una sola
/// familia (PowerShell, cmd, WSL, etc.) y devuelve los perfiles que pudo
/// resolver completamente — los no resolubles se descartan en origen, así
/// nunca llegan a la UI entradas "(no soportado)".
///
/// Implementaciones esperadas (Fase 20.A):
///   WindowsPowerShellDetector, PwshDetector, CmdDetector, WslDetector,
///   GitBashDetector, VsDeveloperDetector.
/// </summary>
public interface IProfileDetector
{
    IReadOnlyList<UserProfile> Detect();
}
