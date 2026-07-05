import { z } from 'zod';

const invoiceLineSchema = z.object({
    description: z.string(),
    quantity: z.number().min(0),
    unitPrice: z.number(),
    taxRate: z.number().min(0).max(100),
    surchargeRate: z.number().min(0).max(100),
    tipoOperacion: z.string(),
}).superRefine((line, ctx) => {
    const hasContent = line.description.trim() || line.unitPrice > 0 || line.quantity > 0;
    if (hasContent && !line.description.trim()) {
        ctx.addIssue({ code: 'custom', message: 'Descripción obligatoria en cada línea', path: ['description'] });
    }
});

export const invoiceCreateSchema = z.object({
    clientType: z.enum(['Registered', 'Manual']),
    clientId: z.string().optional(),
    clientName: z.string().optional(),
    clientTaxId: z.string().optional(),
    clientEmail: z.string().optional(),
    clientAddress: z.string().optional(),
    series: z.string().min(1),
    dueDate: z.string().optional(),
    irpfRate: z.number().min(0).max(100),
    invoiceType: z.string().min(1),
    currencyCode: z.string().min(3).max(3).optional(),
    lines: z.array(invoiceLineSchema).min(1),
}).superRefine((data, ctx) => {
    if (data.clientType === 'Registered' && !data.clientId) {
        ctx.addIssue({ code: 'custom', message: 'Selecciona un cliente', path: ['clientId'] });
    }
    if (data.clientType === 'Manual' && !data.clientName?.trim()) {
        ctx.addIssue({ code: 'custom', message: 'Introduce el nombre del cliente', path: ['clientName'] });
    }
    if (data.lines.every((l) => !l.description.trim())) {
        ctx.addIssue({ code: 'custom', message: 'Añade al menos una línea con descripción', path: ['lines'] });
    }
});

export type InvoiceCreateFormValues = z.infer<typeof invoiceCreateSchema>;
