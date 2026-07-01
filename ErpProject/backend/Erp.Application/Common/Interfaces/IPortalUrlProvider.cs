namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Proporciona la URL base del portal público de la aplicación.
/// Se usa para construir enlaces que se envían a clientes externos (ej. presupuestos).
/// </summary>
public interface IPortalUrlProvider
{
    string PortalBaseUrl { get; }
}
