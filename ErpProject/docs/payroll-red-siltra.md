# Export RED/SILTRA (Payroll) — formato orientativo

Este documento describe el fichero generado por `GET /api/payroll/export/red` y sus
limitaciones respecto al Sistema RED/SILTRA oficial de la TGSS.

## Disclaimer obligatorio

**Este export NO está homologado por la TGSS.** No sustituye:

- Los ficheros XML del Sistema de Liquidación Directa (SILTRA/CRETA).
- La firma con certificado digital ni el envío por RED oficial.
- La validación de un software de nóminas certificado.

Úselo como apoyo a asesoría, conciliación interna o importación manual en
herramientas que acepten texto plano. Contraste siempre con SILTRA o su gestoría
antes de cualquier remisión a la Seguridad Social.

La respuesta HTTP incluye cabeceras `X-Fiscal-Official-Format: false` y
`X-Fiscal-Export-Disclaimer` (véase `FiscalExportHeaders`).

## Formato generado

| Propiedad | Valor |
|-----------|-------|
| Codificación | ISO-8859-1 |
| Longitud de línea | 250 caracteres (relleno con espacios) |
| Fin de línea | LF (`\n`) |
| Registros | `01` cabecera, `02` trabajadores, `99` cierre |

### Registro 01 — Cabecera empresa

| Campo | Pos. (aprox.) | Long. | Origen |
|-------|---------------|-------|--------|
| Tipo registro | 1-2 | 2 | `01` |
| CCC | 3-13 | 11 | Empresa (derivado o módulo 97) |
| Periodo | 14-19 | 6 | `AAAAMM` liquidación finalizada |
| Régimen | 20-21 | 2 | `01` (general, orientativo) |
| Tipo liquidación | 22-24 | 3 | `L00` (mensual, orientativo) |
| NIF empresa | 25-33 | 9 | `Company.TaxId` |
| Razón social | 34-73 | 40 | `Company.Name` |
| Nº trabajadores | 74-79 | 6 | Líneas liquidación `Final` |
| Reserva | 80-250 | — | Espacios |

### Registro 02 — Trabajador

| Campo | Long. | Origen |
|-------|-------|--------|
| Tipo `02` | 2 | Constante |
| NAF | 12 | `Employee.SocialSecurityNumber` (validado módulo 97) |
| NIF | 9 | `Employee.TaxId` |
| Nombre | 40 | `Employee.FullName` |
| Base CC | 11 | `PayrollLine.CommonContingenciesBase` (céntimos, ceros izda.) |
| Base AT/EP | 11 | Igual que base CC (orientativo) |
| Cuota obrera | 11 | `PayrollLine.EmployeeSocialSecurity` |
| Cuota empresa | 11 | `PayrollLine.EmployerSocialSecurity` |
| Bruto | 11 | `PayrollLine.GrossSalary` |
| Días cotizados | 2 | `30` (orientativo; no calcula calendario real) |
| Reserva | — | Espacios hasta 250 |

### Registro 99 — Cierre

Totales de bases y cuotas, recuento de trabajadores y de registros (cabecera +
trabajadores + cierre).

## Validación pre-export

El handler `ExportRedHandler` rechaza (`400 Bad Request`) cuando:

- Mes fuera de 1–12 o año fuera de 2000–2099.
- NIF/CIF de empresa inválido (`SpanishTaxIdValidator`).
- Algún trabajador en liquidación `Final` tiene NIF inválido, NAF vacío o NAF con
  dígito de control incorrecto (`SpanishSocialSecurityNumberValidator`, módulo 97).

Si no hay líneas en liquidaciones `Final` para el periodo, se generan solo registros
`01` y `99` (recuento 0).

## CCC empresa

El CCC de 11 dígitos se resuelve así:

1. Si los dígitos del CIF ya forman un CCC válido → se usa tal cual.
2. Si hay 9 dígitos base (provincia + secuencial) → se calculan los 2 dígitos de
   control (módulo 97).
3. En caso contrario → CCC **orientativo** derivado del CIF (`00` + 7 dígitos del
   CIF + control). **No es el CCC real asignado por la TGSS.**

## TC1 / TC2 (CSV orientativo)

Los endpoints `GET /api/payroll/export/tc1` y `export/tc2` comparten la carga de
líneas y metadatos de empresa (NIF + CCC orientativo). Siguen siendo CSV UTF-8 con
BOM, no ficheros RED.

## SILTRA oficial vs este export

| Aspecto | SILTRA/RED oficial | Este ERP |
|---------|-------------------|----------|
| Formato | XML esquematizado TGSS | Texto plano 250 chars |
| Homologación | Software certificado | Ninguna |
| Firma / envío | Certificado digital | Solo descarga |
| Bases / tramos | Cálculo normativo completo | Importes manuales en `PayrollLine` |
| Altas/bajas/IT | Integrado | No cubierto |

## Código de referencia

- `RedSiltraFileBuilder` — generación de registros.
- `PayrollRedExportValidator` — validación periodo, empresa y NAF.
- `SpanishSocialSecurityNumberValidator` — módulo 97 NAF/CCC.
- Tests: `RedSiltraFileBuilderTests`, `ExportRedHandlerTests`,
  `SpanishSocialSecurityNumberValidatorTests`.

## Frontend

La página `/payroll` incluye el botón **RED/SILTRA** que descarga vía
`/api/proxy/payroll/export/red?year=&month=` tras finalizar la liquidación.
