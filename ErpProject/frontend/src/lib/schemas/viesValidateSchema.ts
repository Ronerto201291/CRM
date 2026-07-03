import { z } from 'zod';

export const viesValidateSchema = z.object({
    countryCode: z.string().length(2, 'Código de país ISO-2 (ej. ES, DE)'),
    vatNumber: z.string().min(1, 'Introduce el NIF/VAT del operador UE'),
});

export type ViesValidateFormValues = z.infer<typeof viesValidateSchema>;
