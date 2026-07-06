import { z } from 'zod';

const salesOrderLineSchema = z.object({
    description: z.string(),
    quantity: z.number().min(0),
    unitPrice: z.number(),
    taxRate: z.number().min(0).max(100),
    productId: z.string().optional(),
}).superRefine((line, ctx) => {
    if ((line.description.trim() || line.unitPrice > 0) && !line.description.trim()) {
        ctx.addIssue({ code: 'custom', message: 'Descripción obligatoria', path: ['description'] });
    }
});

export const salesOrderCreateSchema = z.object({
    customerId: z.string().min(1, 'Selecciona un cliente'),
    number: z.string().min(1, 'Número de pedido obligatorio'),
    orderDate: z.string().min(1),
    notes: z.string().optional(),
    lines: z.array(salesOrderLineSchema).min(1),
}).superRefine((data, ctx) => {
    if (data.lines.every((l) => !l.description.trim())) {
        ctx.addIssue({ code: 'custom', message: 'Añade al menos una línea con descripción', path: ['lines'] });
    }
});

export type SalesOrderCreateFormValues = z.infer<typeof salesOrderCreateSchema>;
