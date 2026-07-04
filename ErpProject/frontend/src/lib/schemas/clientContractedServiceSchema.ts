import { z } from 'zod';

export const clientContractedServiceFormSchema = z.object({
    serviceCatalogItemId: z.string().min(1, 'Selecciona un servicio del catálogo'),
    priceOverride: z.coerce.number().min(0, 'El precio no puede ser negativo').optional(),
    taxRateOverride: z.coerce.number().min(0).max(100).optional(),
    periodicityOverride: z.enum(['Monthly', 'Quarterly', 'Yearly']).optional(),
    startDate: z.string().min(1, 'La fecha de inicio es obligatoria'),
});

export type ClientContractedServiceFormValues = z.infer<typeof clientContractedServiceFormSchema>;
