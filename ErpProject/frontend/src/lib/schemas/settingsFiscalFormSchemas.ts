import { z } from 'zod';

export const siiSubmitSchema = z.object({
    type: z.enum(['emitidas', 'recibidas']),
    year: z.number().int().min(2000).max(2100),
    month: z.number().int().min(1).max(12),
});

export const verifactuSubmitSchema = z.object({
    year: z.number().int().min(2000).max(2100),
    month: z.number().int().min(1).max(12),
});

export const fiscalEventCreateSchema = z.object({
    eventType: z.string().min(1, 'Tipo de evento obligatorio'),
    description: z.string().min(1, 'Descripción obligatoria'),
    amount: z.string().optional(),
});

export const settingsSiiSchema = z.object({
    enabled: z.boolean(),
    nif: z.string().min(1, 'NIF obligatorio'),
    certificateAlias: z.string().optional(),
});

export const notificationsSettingsSchema = z.object({
    frequency: z.enum(['daily', 'weekly', 'instant', 'never']),
});

export const companySettingsSchema = z.object({
    name: z.string().min(1, 'Nombre de empresa obligatorio'),
    taxId: z.string().min(1, 'CIF/NIF obligatorio'),
    address: z.string().optional(),
    country: z.string().optional(),
});

export const accountantExportSettingsSchema = z.object({
    accountantEmail: z.string().email('Email inválido').optional().or(z.literal('')),
    frequency: z.enum(['disabled', 'daily', 'weekly', 'monthly']),
});

export const totpCodeSchema = z.object({
    code: z.string().min(6, 'Código de 6 dígitos').max(6),
});

export type SiiSubmitFormValues = z.infer<typeof siiSubmitSchema>;
