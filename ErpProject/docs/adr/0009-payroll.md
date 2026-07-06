# ADR-0009: Payroll (Nóminas)

## Estado
Aceptado — refleja la implementación actual del código en `main` (**Fase 2** jul 2026).

## Contexto
El módulo Payroll gestiona trabajadores y liquidaciones mensuales de
nómina (bases de cotización TGSS, retención IRPF, líquido a percibir), y
genera exportes CSV/XML orientativos para TC1/TC2, fichero RED texto plano
(ISO-8859-1) orientativo, **PDF recibo de nómina orientativo** y exportes
CSV orientativos de **modelos 111/190** desde datos de nómina — **no homologado
TGSS ni presentables en AEAT** (ver `docs/payroll-red-siltra.md`). Sigue la
estructura estándar de módulo descrita en ADR-0001.

**Fase 1 (jul 2026):** cálculo básico con plantillas configurables (SS/IRPF),
entidades de concepto con API, disclaimers legales en UI.
**Fase 2 (jul 2026):** PDF recibo por línea (`GET .../lines/{id}/pdf` vía
QuestPDF), exportes orientativos modelos 111 (trimestral) y 190 (anual),
listado de líneas por liquidación para descarga en UI, validación Zod en
formularios frontend.

## Decisión

### Backend
`PayrollController` (`backend/Modules/Payroll/Api/Controllers/PayrollController.cs`,
ruta base `api/payroll`, `[Authorize]`) expone **18 endpoints**:

- Fase 0/1: empleados, liquidaciones, plantillas, cálculo, conceptos, exports TC1/TC2/RED.
- **Fase 2:**
  - `GET /api/payroll/settlements/{id}/lines` — líneas con empleado/importes (`GetSettlementLinesQuery`).
  - `GET /api/payroll/lines/{lineId}/pdf` — PDF recibo orientativo (`GetPayrollLinePdfQuery` +
    `IPayrollPayslipPdfService` / `PayrollPayslipPdfService` en Infrastructure, QuestPDF).
  - `GET /api/payroll/export/model-111?year=&quarter=` — CSV orientativo retenciones trimestrales.
  - `GET /api/payroll/export/model-190?year=` — CSV orientativo resumen anual por trabajador.

Todos los exportes fiscales usan `FiscalExportHeaders.MarkAsNonOfficial` + disclaimer en respuesta.

No hay endpoints para editar/borrar trabajadores o líneas, ni para
reabrir una liquidación `Final`.

### Frontend
`frontend/src/app/payroll/PayrollClient.tsx`: título **"Nóminas (Fase 2)"**.
Botones **Descargar PDF** por línea (expandir liquidación → recibos),
**Exportar Modelo 111/190** con `LegalDisclaimer` dedicado.
Validación Zod en `lib/schemas/payrollFormSchemas.ts`.
Disclaimers: `LegalDisclaimer` (módulo, cálculo, exports, 111/190) +
lectura cabecera `X-Fiscal-Export-Disclaimer` tras descarga.

### Modelo de datos
Sin cambios de esquema en Fase 2 (reutiliza `PayrollLine`, `Employee`, `PayrollSettlement`).

### Flujo end-to-end representativo (Fase 2)
1. Usuario finaliza liquidación (Fase 0/1).
2. Expande liquidación en UI → ve líneas por empleado.
3. Pulsa **Descargar PDF** → `GET /lines/{id}/pdf` → recibo orientativo con disclaimer embebido.
4. Para AEAT orientativo: exporta Modelo 111 (trimestre) o 190 (año) → CSV con disclaimer HTTP.

## Relación con otros módulos
- **Accounting (ADR-0006):** integración vía `IPayrollJournalEntryGenerator` (#19c).
- Modelos 111/190 desde nómina son **orientativos**; la presentación oficial AEAT puede
  requerir datos adicionales no modelados (pagos a terceros, etc.).

## Evaluación de calidad arquitectónica
> Metodología completa en `ADR-0018`.

Payroll cumple CQRS: `Features/Pdf/`, `Features/Exports/` (111/190), `Features/Settlements/`
(líneas por liquidación). Controller delgado vía `IMediator` (#10 ✅).
PDF en Infrastructure con interfaz en Application (mismo patrón que Billing PDF).

## Buenas prácticas aplicables
- PDF y modelos 111/190: disclaimers obligatorios — no fingir homologación TGSS/AEAT.
- Tests: `PayrollPhase2HandlerTests` (PDF bytes, Modelo 111 CSV).

## Consecuencias
- **Fase 2 completa:** PDF recibo, 111/190 orientativos, UI conectada, tests unitarios.
- Importes y exportes siguen siendo orientativos; revisión por gestoría obligatoria.
- Sin bajas médicas, vacaciones, finiquitos, SILTRA homologado.
- Integración contable vía puerto #19c sin cambios.
