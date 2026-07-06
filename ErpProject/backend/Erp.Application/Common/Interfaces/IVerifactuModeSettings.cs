namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Modo VERI*FACTU (RD 1007/2023): remisión TIKE en tiempo real o registro local sin envío.
/// </summary>
public interface IVerifactuModeSettings
{
  /// <summary>Si true, encola envío TIKE; si false, modo local (sin remisión en expedición).</summary>
  bool RealtimeSubmissionEnabled { get; }
}
