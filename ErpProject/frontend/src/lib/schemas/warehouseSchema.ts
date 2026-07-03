import { z } from 'zod';

export const warehouseFormSchema = z.object({
    name: z.string().min(1, 'El nombre es obligatorio'),
    location: z.string().optional(),
});

export type WarehouseFormValues = z.infer<typeof warehouseFormSchema>;
