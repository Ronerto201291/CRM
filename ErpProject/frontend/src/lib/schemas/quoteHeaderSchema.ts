import { z } from 'zod';

export const quoteHeaderSchema = z.object({
    clientType: z.enum(['Registered', 'Lead', 'Manual']),
    clientId: z.string().optional(),
    clientName: z.string().optional(),
    clientTaxId: z.string().optional(),
    clientEmail: z.string().email('Email inválido').optional().or(z.literal('')),
    clientPhone: z.string().optional(),
    clientAddress: z.string().optional(),
    issueDate: z.string().min(1),
    validUntil: z.string().min(1),
    globalDiscountPct: z.number().min(0).max(100),
    notes: z.string().optional(),
    internalNotes: z.string().optional(),
}).superRefine((data, ctx) => {
    if (data.clientType === 'Registered' && !data.clientId) {
        ctx.addIssue({ code: 'custom', message: 'Selecciona un cliente', path: ['clientId'] });
    }
    if (data.clientType === 'Lead' && !data.clientId) {
        ctx.addIssue({ code: 'custom', message: 'Selecciona un posible cliente', path: ['clientId'] });
    }
    if (data.clientType === 'Manual' && !data.clientName?.trim()) {
        ctx.addIssue({ code: 'custom', message: 'Introduce el nombre del cliente', path: ['clientName'] });
    }
});

export type QuoteHeaderFormValues = z.infer<typeof quoteHeaderSchema>;
