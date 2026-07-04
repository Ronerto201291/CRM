import { describe, expect, it } from 'vitest';
import { loginSchema, signupSchema, totpSchema } from '@/lib/schemas/authSchemas';

describe('loginSchema', () => {
    it('acepta credenciales válidas', () => {
        const result = loginSchema.safeParse({ email: 'user@test.com', password: 'secret' });
        expect(result.success).toBe(true);
    });

    it('rechaza email inválido', () => {
        const result = loginSchema.safeParse({ email: 'no-email', password: 'secret' });
        expect(result.success).toBe(false);
    });

    it('rechaza contraseña vacía', () => {
        const result = loginSchema.safeParse({ email: 'user@test.com', password: '' });
        expect(result.success).toBe(false);
    });
});

describe('totpSchema', () => {
    it('acepta código de 6 dígitos', () => {
        const result = totpSchema.safeParse({ code: '123456', userId: 'abc' });
        expect(result.success).toBe(true);
    });

    it('rechaza código corto', () => {
        const result = totpSchema.safeParse({ code: '12345', userId: 'abc' });
        expect(result.success).toBe(false);
    });
});

describe('signupSchema', () => {
    it('acepta registro completo con contraseñas coincidentes', () => {
        const result = signupSchema.safeParse({
            companyName: 'Mi SL',
            companyTaxId: 'B12345678',
            adminEmail: 'admin@test.com',
            adminFirstName: 'Ana',
            adminPassword: 'SecurePass1',
            confirmPassword: 'SecurePass1',
        });
        expect(result.success).toBe(true);
    });

    it('rechaza contraseñas distintas', () => {
        const result = signupSchema.safeParse({
            companyName: 'Mi SL',
            companyTaxId: 'B12345678',
            adminEmail: 'admin@test.com',
            adminFirstName: 'Ana',
            adminPassword: 'SecurePass1',
            confirmPassword: 'OtraPass1',
        });
        expect(result.success).toBe(false);
    });
});
