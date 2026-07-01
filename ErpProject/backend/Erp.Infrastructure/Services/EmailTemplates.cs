namespace Erp.Infrastructure.Services;

/// <summary>
/// Generates responsive HTML email templates with professional design.
/// All templates share the same base layout for brand consistency.
/// </summary>
internal static class EmailTemplates
{
    // ─── Base Layout ────────────────────────────────────────────────────────────

    private static string Wrap(string companyName, string bodyContent) => $"""
        <!DOCTYPE html>
        <html lang="es">
        <head>
          <meta charset="UTF-8" />
          <meta name="viewport" content="width=device-width,initial-scale=1.0" />
          <title>{companyName}</title>
        </head>
        <body style="margin:0;padding:0;background:#f4f6f9;font-family:'Segoe UI',Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" role="presentation" style="background:#f4f6f9;">
            <tr><td align="center" style="padding:32px 16px;">

              <!-- Card -->
              <table width="600" cellpadding="0" cellspacing="0" role="presentation"
                     style="background:#ffffff;border-radius:8px;box-shadow:0 2px 12px rgba(0,0,0,.08);overflow:hidden;max-width:600px;width:100%;">

                <!-- Header -->
                <tr>
                  <td style="background:linear-gradient(135deg,#1a56db 0%,#1e40af 100%);padding:32px 40px;">
                    <table width="100%" cellpadding="0" cellspacing="0" role="presentation">
                      <tr>
                        <td>
                          <span style="color:#ffffff;font-size:22px;font-weight:700;letter-spacing:-0.3px;">{companyName}</span>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>

                <!-- Body -->
                <tr>
                  <td style="padding:40px 40px 32px;">
                    {bodyContent}
                  </td>
                </tr>

                <!-- Footer -->
                <tr>
                  <td style="background:#f8fafc;padding:24px 40px;border-top:1px solid #e2e8f0;">
                    <p style="margin:0;font-size:12px;color:#94a3b8;line-height:1.6;">
                      Este mensaje ha sido enviado de forma automática. Por favor, no responda a este correo.<br/>
                      Si tiene alguna duda, contacte con soporte en <a href="mailto:soporte@example.com"
                      style="color:#1a56db;text-decoration:none;">soporte@example.com</a>
                    </p>
                  </td>
                </tr>

              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    // ─── Invoice Template ────────────────────────────────────────────────────────

    public static string Invoice(
        string toName,
        string invoiceNumber,
        DateTime issueDate,
        DateTime dueDate,
        decimal total,
        string companyName,
        bool hasPdf)
    {
        var attachment = hasPdf
            ? "<p style='margin:12px 0 0;font-size:14px;color:#64748b;'>📎 Encontrará la factura en PDF adjunta a este correo.</p>"
            : string.Empty;

        var body = $"""
            <h1 style="margin:0 0 8px;font-size:24px;font-weight:700;color:#0f172a;">
              Factura disponible
            </h1>
            <p style="margin:0 0 28px;font-size:15px;color:#475569;line-height:1.6;">
              Estimado/a <strong>{toName}</strong>,<br/>
              Le adjuntamos la factura correspondiente a los servicios prestados.
            </p>

            <!-- Invoice Details Card -->
            <table width="100%" cellpadding="0" cellspacing="0" role="presentation"
                   style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:6px;margin-bottom:28px;">
              <tr>
                <td style="padding:24px 28px;">
                  <table width="100%" cellpadding="0" cellspacing="0" role="presentation">
                    <tr>
                      <td style="padding-bottom:14px;border-bottom:1px solid #e2e8f0;">
                        <span style="font-size:12px;font-weight:600;color:#94a3b8;text-transform:uppercase;letter-spacing:.6px;">Número de factura</span><br/>
                        <span style="font-size:18px;font-weight:700;color:#1a56db;letter-spacing:-.2px;">{invoiceNumber}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:14px 0 0;">
                        <table width="100%" cellpadding="0" cellspacing="0" role="presentation">
                          <tr>
                            <td style="width:50%;vertical-align:top;">
                              <span style="font-size:12px;font-weight:600;color:#94a3b8;text-transform:uppercase;letter-spacing:.6px;">Fecha de emisión</span><br/>
                              <span style="font-size:14px;color:#0f172a;font-weight:500;">{issueDate:dd/MM/yyyy}</span>
                            </td>
                            <td style="width:50%;vertical-align:top;">
                              <span style="font-size:12px;font-weight:600;color:#94a3b8;text-transform:uppercase;letter-spacing:.6px;">Fecha de vencimiento</span><br/>
                              <span style="font-size:14px;color:#dc2626;font-weight:500;">{dueDate:dd/MM/yyyy}</span>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>

            <!-- Total -->
            <table width="100%" cellpadding="0" cellspacing="0" role="presentation"
                   style="background:linear-gradient(135deg,#1a56db 0%,#1e40af 100%);border-radius:6px;margin-bottom:8px;">
              <tr>
                <td style="padding:20px 28px;">
                  <table width="100%" cellpadding="0" cellspacing="0" role="presentation">
                    <tr>
                      <td>
                        <span style="font-size:13px;color:rgba(255,255,255,.75);font-weight:500;">TOTAL A PAGAR (IVA incl.)</span>
                      </td>
                      <td align="right">
                        <span style="font-size:26px;font-weight:700;color:#ffffff;">{total:N2} €</span>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            {attachment}
            <p style="margin:28px 0 0;font-size:14px;color:#64748b;line-height:1.7;">
              Si tiene alguna pregunta sobre esta factura, no dude en ponerse en contacto con nosotros.
            </p>
            """;

        return Wrap(companyName, body);
    }

    // ─── Password Reset Template ─────────────────────────────────────────────────

    public static string PasswordReset(string toName, string resetUrl, string companyName)
    {
        var body = $"""
            <h1 style="margin:0 0 8px;font-size:24px;font-weight:700;color:#0f172a;">
              Restablecer contraseña
            </h1>
            <p style="margin:0 0 28px;font-size:15px;color:#475569;line-height:1.6;">
              Hola <strong>{toName}</strong>,<br/>
              Recibimos una solicitud para restablecer la contraseña de tu cuenta.
              Haz clic en el botón de abajo para continuar. Si no realizaste esta solicitud, ignora este correo.
            </p>

            <!-- CTA Button -->
            <table cellpadding="0" cellspacing="0" role="presentation" style="margin:0 0 28px;">
              <tr>
                <td style="border-radius:6px;background:#1a56db;">
                  <a href="{resetUrl}"
                     style="display:inline-block;padding:14px 32px;font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:6px;letter-spacing:-.1px;">
                    Restablecer contraseña
                  </a>
                </td>
              </tr>
            </table>

            <!-- Fallback URL -->
            <p style="margin:0 0 8px;font-size:13px;color:#94a3b8;">
              Si el botón no funciona, copia y pega esta URL en tu navegador:
            </p>
            <p style="margin:0 0 28px;font-size:13px;word-break:break-all;">
              <a href="{resetUrl}" style="color:#1a56db;text-decoration:none;">{resetUrl}</a>
            </p>

            <!-- Expiry Warning -->
            <table width="100%" cellpadding="0" cellspacing="0" role="presentation"
                   style="background:#fef9c3;border:1px solid #fde047;border-radius:6px;">
              <tr>
                <td style="padding:14px 20px;">
                  <p style="margin:0;font-size:13px;color:#854d0e;line-height:1.5;">
                    ⏱ Este enlace es válido durante <strong>1 hora</strong> y solo puede usarse una vez.
                  </p>
                </td>
              </tr>
            </table>
            """;

        return Wrap(companyName, body);
    }

    // ─── Quote Template ──────────────────────────────────────────────────────────

    public static string Quote(
        string toName,
        string quoteNumber,
        DateTime issueDate,
        DateTime validUntil,
        decimal totalAmount,
        string companyName,
        string portalUrl,
        bool hasPdf)
    {
        var days = (validUntil.Date - issueDate.Date).Days;
        var validityText = days > 0
            ? $"Este presupuesto es válido durante <strong>{days} días</strong>."
            : "Este presupuesto ha expirado.";
        var attachment = hasPdf
            ? "<p style='margin:12px 0 0;font-size:14px;color:#64748b;'>📎 Encontrará el presupuesto en PDF adjunto a este correo.</p>"
            : string.Empty;

        var body = $"""
            <h1 style="margin:0 0 8px;font-size:24px;font-weight:700;color:#0f172a;">
              Presupuesto disponible para su revisión
            </h1>
            <p style="margin:0 0 28px;font-size:15px;color:#475569;line-height:1.6;">
              Estimado/a <strong>{toName}</strong>,<br/>
              Le enviamos el presupuesto solicitado. Puede revisarlo y aceptarlo
              o rechazarlo directamente desde el botón a continuación.
            </p>

            <!-- Quote Details Card -->
            <table width="100%" cellpadding="0" cellspacing="0" role="presentation"
                   style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:6px;margin-bottom:28px;">
              <tr>
                <td style="padding:24px 28px;">
                  <table width="100%" cellpadding="0" cellspacing="0" role="presentation">
                    <tr>
                      <td style="padding-bottom:14px;border-bottom:1px solid #e2e8f0;">
                        <span style="font-size:12px;font-weight:600;color:#94a3b8;text-transform:uppercase;letter-spacing:.6px;">Número de presupuesto</span><br/>
                        <span style="font-size:18px;font-weight:700;color:#1a56db;letter-spacing:-.2px;">{quoteNumber}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:14px 0 0;">
                        <table width="100%" cellpadding="0" cellspacing="0" role="presentation">
                          <tr>
                            <td style="width:50%;vertical-align:top;">
                              <span style="font-size:12px;font-weight:600;color:#94a3b8;text-transform:uppercase;letter-spacing:.6px;">Fecha de emisión</span><br/>
                              <span style="font-size:14px;color:#0f172a;font-weight:500;">{issueDate:dd/MM/yyyy}</span>
                            </td>
                            <td style="width:50%;vertical-align:top;">
                              <span style="font-size:12px;font-weight:600;color:#94a3b8;text-transform:uppercase;letter-spacing:.6px;">Válido hasta</span><br/>
                              <span style="font-size:14px;color:#dc2626;font-weight:500;">{validUntil:dd/MM/yyyy}</span>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>

            <!-- Total -->
            <table width="100%" cellpadding="0" cellspacing="0" role="presentation"
                   style="background:linear-gradient(135deg,#1a56db 0%,#1e40af 100%);border-radius:6px;margin-bottom:28px;">
              <tr>
                <td style="padding:20px 28px;">
                  <table width="100%" cellpadding="0" cellspacing="0" role="presentation">
                    <tr>
                      <td><span style="font-size:13px;color:rgba(255,255,255,.75);font-weight:500;">TOTAL PRESUPUESTADO (IVA incl.)</span></td>
                      <td align="right"><span style="font-size:26px;font-weight:700;color:#ffffff;">{totalAmount:N2} €</span></td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>

            <!-- CTA -->
            <table cellpadding="0" cellspacing="0" role="presentation" style="margin:0 0 12px;">
              <tr>
                <td style="border-radius:6px;background:#16a34a;">
                  <a href="{portalUrl}"
                     style="display:inline-block;padding:14px 32px;font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:6px;">
                    Ver y responder al presupuesto
                  </a>
                </td>
              </tr>
            </table>
            {attachment}

            <table width="100%" cellpadding="0" cellspacing="0" role="presentation"
                   style="background:#fef9c3;border:1px solid #fde047;border-radius:6px;margin-top:24px;">
              <tr>
                <td style="padding:14px 20px;">
                  <p style="margin:0;font-size:13px;color:#854d0e;line-height:1.5;">
                    ⏱ {validityText} Fecha límite: <strong>{validUntil:dd/MM/yyyy}</strong>.
                  </p>
                </td>
              </tr>
            </table>
            """;

        return Wrap(companyName, body);
    }

    // ─── Email Confirmation Template ─────────────────────────────────────────────

    public static string EmailConfirmation(string toName, string confirmUrl, string companyName)
    {
        var body = $"""
            <h1 style="margin:0 0 8px;font-size:24px;font-weight:700;color:#0f172a;">
              Confirma tu dirección de correo
            </h1>
            <p style="margin:0 0 28px;font-size:15px;color:#475569;line-height:1.6;">
              ¡Bienvenido/a <strong>{toName}</strong>!<br/>
              Gracias por registrarte. Confirma tu dirección de correo electrónico para activar tu cuenta
              y empezar a utilizar todas las funcionalidades.
            </p>

            <!-- CTA Button -->
            <table cellpadding="0" cellspacing="0" role="presentation" style="margin:0 0 28px;">
              <tr>
                <td style="border-radius:6px;background:#16a34a;">
                  <a href="{confirmUrl}"
                     style="display:inline-block;padding:14px 32px;font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:6px;letter-spacing:-.1px;">
                    Confirmar dirección de correo
                  </a>
                </td>
              </tr>
            </table>

            <!-- Fallback URL -->
            <p style="margin:0 0 8px;font-size:13px;color:#94a3b8;">
              Si el botón no funciona, copia y pega esta URL en tu navegador:
            </p>
            <p style="margin:0 0 28px;font-size:13px;word-break:break-all;">
              <a href="{confirmUrl}" style="color:#16a34a;text-decoration:none;">{confirmUrl}</a>
            </p>

            <!-- Info Box -->
            <table width="100%" cellpadding="0" cellspacing="0" role="presentation"
                   style="background:#f0fdf4;border:1px solid #86efac;border-radius:6px;">
              <tr>
                <td style="padding:14px 20px;">
                  <p style="margin:0;font-size:13px;color:#166534;line-height:1.5;">
                    ✅ Este enlace es válido durante <strong>24 horas</strong>.
                    Si no creaste esta cuenta, puedes ignorar este correo de forma segura.
                  </p>
                </td>
              </tr>
            </table>
            """;

        return Wrap(companyName, body);
    }
}
