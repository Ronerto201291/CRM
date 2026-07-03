import { z } from 'zod';

export const productFormSchema = z.object({
    name: z.string().min(1, 'El nombre es obligatorio'),
    sku: z.string().optional(),
    price: z.number().min(0, 'El precio no puede ser negativo'),
    taxRate: z.number().min(0).max(100),
    reorderPoint: z.number().min(0),
    reorderQty: z.number().min(0),
});

export type ProductFormValues = z.infer<typeof productFormSchema>;
