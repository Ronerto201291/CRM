import { z } from 'zod';

export const serviceCatalogFormSchema = z.object({
    name: z.string().min(1, 'El nombre es obligatorio'),
    description: z.string().optional(),
    defaultPrice: z.coerce.number().min(0, 'El precio no puede ser negativo'),
    defaultTaxRate: z.coerce.number().min(0, 'El IVA no puede ser negativo').max(100, 'El IVA no puede superar 100'),
    defaultPeriodicity: z.enum(['Monthly', 'Quarterly', 'Yearly']),
});

export type ServiceCatalogFormValues = z.infer<typeof serviceCatalogFormSchema>;
