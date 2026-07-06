import { z } from 'zod';

export const prospectFormSchema = z.object({
    name: z.string().min(1, 'El nombre es obligatorio'),
    email: z.string().email('Email inválido').optional().or(z.literal('')),
    phone: z.string().optional(),
    taxId: z.string().optional(),
    address: z.string().optional(),
    status: z.string().min(1),
    source: z.string().optional(),
    notes: z.string().optional(),
});

export type ProspectFormValues = z.infer<typeof prospectFormSchema>;
