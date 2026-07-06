import { z } from 'zod';

const positiveDecimalString = (label: string) =>
    z.string().min(1, `${label} obligatorio`).refine(
        v => !Number.isNaN(parseFloat(v)) && parseFloat(v) >= 0,
        `${label} debe ser un número válido`,
    );

export const payrollEmployeeSchema = z.object({
    taxId: z.string().min(1, 'NIF obligatorio'),
    fullName: z.string().min(1, 'Nombre obligatorio'),
    ss: z.string().optional(),
});

export const payrollSettlementSchema = z.object({
    year: z.number().int().min(2000).max(2100),
    month: z.number().int().min(1).max(12),
});

export const payrollTemplateSchema = z.object({
    name: z.string().min(1, 'Nombre de plantilla obligatorio'),
    empSs: positiveDecimalString('SS obrera'),
    erSs: positiveDecimalString('SS empresa'),
    irpf: positiveDecimalString('IRPF'),
});

export const payrollCalculateLineSchema = z.object({
    settlementId: z.string().min(1, 'Selecciona liquidación'),
    employeeId: z.string().min(1, 'Selecciona empleado'),
    gross: positiveDecimalString('Bruto'),
    templateId: z.string().optional(),
    irpfOverride: z.string().optional(),
});

export const payrollManualLineSchema = z.object({
    settlementId: z.string().min(1, 'Selecciona liquidación'),
    employeeId: z.string().min(1, 'Selecciona empleado'),
    gross: positiveDecimalString('Bruto'),
    baseCc: positiveDecimalString('Base CC'),
    empSs: positiveDecimalString('SS obrera'),
    erSs: positiveDecimalString('SS empresa'),
    irpfBase: positiveDecimalString('Base IRPF'),
    irpfRate: positiveDecimalString('% IRPF'),
    irpfW: positiveDecimalString('IRPF retenido'),
    net: positiveDecimalString('Líquido'),
});

export const payrollModel111ExportSchema = z.object({
    year: z.number().int().min(2000).max(2100),
    quarter: z.number().int().min(1).max(4),
});

export const payrollModel190ExportSchema = z.object({
    year: z.number().int().min(2000).max(2100),
});

export type PayrollEmployeeFormValues = z.infer<typeof payrollEmployeeSchema>;
export type PayrollSettlementFormValues = z.infer<typeof payrollSettlementSchema>;
export type PayrollTemplateFormValues = z.infer<typeof payrollTemplateSchema>;
