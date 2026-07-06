import { z } from 'zod';

export const loginSchema = z.object({
    email: z.string().email('Email inválido'),
    password: z.string().min(1, 'La contraseña es obligatoria'),
});

export const totpSchema = z.object({
    code: z.string().regex(/^\d{6}$/, 'El código debe tener 6 dígitos'),
    userId: z.string().min(1, 'Sesión 2FA inválida'),
});

export const signupSchema = z.object({
    companyName: z.string().min(1, 'El nombre de la empresa es obligatorio'),
    companyTaxId: z.string().min(1, 'El CIF/NIF es obligatorio'),
    companyAddress: z.string().optional(),
    adminEmail: z.string().email('Email inválido'),
    adminFirstName: z.string().min(1, 'El nombre es obligatorio'),
    adminLastName: z.string().optional(),
    adminPassword: z.string().min(8, 'La contraseña debe tener al menos 8 caracteres'),
    confirmPassword: z.string(),
}).refine(d => d.adminPassword === d.confirmPassword, {
    message: 'Las contraseñas no coinciden',
    path: ['confirmPassword'],
});

export const registerInviteSchema = z.object({
    token: z.string().min(1, 'Token de invitación requerido'),
    taxId: z.string().min(1, 'El CIF/NIF es obligatorio'),
    firstName: z.string().min(1, 'El nombre es obligatorio'),
    lastName: z.string().optional(),
    password: z.string().min(8, 'La contraseña debe tener al menos 8 caracteres'),
    confirmPassword: z.string(),
}).refine(d => d.password === d.confirmPassword, {
    message: 'Las contraseñas no coinciden',
    path: ['confirmPassword'],
});
