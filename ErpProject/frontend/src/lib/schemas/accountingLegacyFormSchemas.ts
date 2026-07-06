import { z } from 'zod';

export const ispCreateSchema = z.object({
    supplierCountryCode: z.string().min(2, 'País obligatorio').max(2),
    vatableBase: z.number().min(0, 'Base imponible no puede ser negativa'),
    vatRate: z.number().min(0).max(100, 'Tipo IVA inválido'),
});

export const recargoCreateSchema = z.object({
    supplierVat: z.string().min(1, 'NIF/CIF proveedor obligatorio'),
    supplierIsRE: z.boolean(),
    baseAmount: z.number().min(0, 'Base imponible no puede ser negativa'),
    rechargeRate: z.number().min(0).max(100, 'Tipo recargo inválido'),
});

export const fiscalCalendarEventSchema = z.object({
    modelCode: z.string().min(1, 'Modelo obligatorio'),
    modelName: z.string().optional(),
    year: z.number().int().min(2000).max(2100),
    quarter: z.string().optional(),
    month: z.string().optional(),
    deadlineDate: z.string().min(1, 'Fecha límite obligatoria'),
    reminderDate: z.string().optional(),
    amount: z.string().optional(),
    notes: z.string().optional(),
});

export const depreciationAssetSchema = z.object({
    assetCode: z.string().min(1, 'Código de activo obligatorio'),
    name: z.string().min(1, 'Nombre obligatorio'),
    acquisitionDate: z.string().min(1, 'Fecha de adquisición obligatoria'),
    acquisitionCost: z.string().min(1, 'Coste obligatorio').refine(
        v => !Number.isNaN(parseFloat(v)) && parseFloat(v) > 0,
        'Coste debe ser mayor que 0',
    ),
    usefulLifeYears: z.string().min(1, 'Vida útil obligatoria').refine(
        v => !Number.isNaN(parseInt(v, 10)) && parseInt(v, 10) > 0,
        'Vida útil debe ser mayor que 0',
    ),
    residualValue: z.string().optional(),
});

export const budgetCreateSchema = z.object({
    name: z.string().min(1, 'Nombre obligatorio'),
    fiscalYear: z.number().int().min(2000).max(2100),
});

export const budgetLineSchema = z.object({
    accountCode: z.string().min(1, 'Cuenta obligatoria'),
    amount: z.string().min(1, 'Importe obligatorio').refine(
        v => !Number.isNaN(parseFloat(v)),
        'Importe debe ser numérico',
    ),
});

export const provisionCreateSchema = z.object({
    code: z.string().min(1, 'Código obligatorio'),
    description: z.string().min(1, 'Descripción obligatoria'),
    amount: z.string().min(1, 'Importe obligatorio').refine(
        v => !Number.isNaN(parseFloat(v)) && parseFloat(v) > 0,
        'Importe debe ser mayor que 0',
    ),
    dueDate: z.string().min(1, 'Fecha vencimiento obligatoria'),
    notes: z.string().optional(),
});

export type IspCreateFormValues = z.infer<typeof ispCreateSchema>;
export type RecargoCreateFormValues = z.infer<typeof recargoCreateSchema>;
