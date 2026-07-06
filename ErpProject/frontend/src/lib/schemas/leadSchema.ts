import { z } from 'zod';

export const leadFormSchema = z.object({
    name: z.string().min(1, 'El nombre es obligatorio'),
    email: z.string().email('Email inválido').optional().or(z.literal('')),
    phone: z.string().optional(),
    status: z.string().min(1),
    source: z.string().optional(),
    notes: z.string().optional(),
});

export type LeadFormValues = z.infer<typeof leadFormSchema>;
