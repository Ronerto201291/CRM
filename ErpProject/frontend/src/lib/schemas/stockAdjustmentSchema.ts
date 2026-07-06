import { z } from 'zod';

export const stockAdjustmentSchema = z.object({
    warehouseId: z.string().min(1, 'Selecciona un almacén'),
    quantity: z.number().positive('La cantidad debe ser mayor que 0'),
    movementType: z.enum(['StockIn', 'StockOut', 'Adjustment']),
    reason: z.string().optional(),
});

export type StockAdjustmentFormValues = z.infer<typeof stockAdjustmentSchema>;
