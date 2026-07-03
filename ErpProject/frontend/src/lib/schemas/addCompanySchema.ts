import { z } from 'zod';

export const addCompanySchema = z.object({
    companyName: z.string().min(2, 'Nombre de empresa requerido'),
    companyTaxId: z.string().min(9, 'CIF/NIF inválido').max(12),
    companyAddress: z.string().optional(),
});

export type AddCompanyFormValues = z.infer<typeof addCompanySchema>;
