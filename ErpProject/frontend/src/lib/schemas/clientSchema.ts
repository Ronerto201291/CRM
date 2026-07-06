import { z } from 'zod';

export const clientFormSchema = z.object({
    name: z.string().min(1, 'El nombre es obligatorio'),
    taxId: z.string().min(1, 'El CIF/NIF es obligatorio'),
    email: z.string().email('Email inválido').optional().or(z.literal('')),
    phone: z.string().optional(),
    address: z.string().optional(),
    city: z.string().optional(),
    zipCode: z.string().optional(),
    country: z.string().optional(),
});

export type ClientFormValues = z.infer<typeof clientFormSchema>;
