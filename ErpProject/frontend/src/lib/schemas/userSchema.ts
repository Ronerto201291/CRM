import { z } from 'zod';

export const createUserSchema = z.object({
    firstName: z.string().min(1, 'El nombre es obligatorio'),
    lastName: z.string().optional(),
    email: z.string().email('Email inválido'),
    roleId: z.string().optional(),
});

export type CreateUserFormValues = z.infer<typeof createUserSchema>;
