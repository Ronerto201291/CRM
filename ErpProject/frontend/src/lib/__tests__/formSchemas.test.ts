import { describe, expect, it } from 'vitest';
import { invoiceCreateSchema } from '@/lib/schemas/invoiceCreateSchema';
import { leadFormSchema } from '@/lib/schemas/leadSchema';
import { expenseCreateSchema } from '@/lib/schemas/expenseCreateSchema';
import { salesOrderCreateSchema } from '@/lib/schemas/salesOrderCreateSchema';
import { createUserSchema } from '@/lib/schemas/userSchema';

describe('invoiceCreateSchema', () => {
    it('acepta factura manual válida', () => {
        const result = invoiceCreateSchema.safeParse({
            clientType: 'Manual',
            clientName: 'Cliente SL',
            clientTaxId: 'B12345678',
            series: 'A',
            dueDate: '2026-12-31',
            irpfRate: 0,
            invoiceType: 'Normal',
            lines: [{
                description: 'Servicio',
                quantity: 1,
                unitPrice: 100,
                taxRate: 21,
                surchargeRate: 0,
                tipoOperacion: 'Nacional',
            }],
        });
        expect(result.success).toBe(true);
    });

    it('rechaza cliente registrado sin clientId', () => {
        const result = invoiceCreateSchema.safeParse({
            clientType: 'Registered',
            series: 'A',
            irpfRate: 0,
            invoiceType: 'Normal',
            lines: [{
                description: 'Servicio',
                quantity: 1,
                unitPrice: 100,
                taxRate: 21,
                surchargeRate: 0,
                tipoOperacion: 'Nacional',
            }],
        });
        expect(result.success).toBe(false);
    });

    it('rechaza líneas sin descripción', () => {
        const result = invoiceCreateSchema.safeParse({
            clientType: 'Manual',
            clientName: 'Cliente',
            series: 'A',
            irpfRate: 0,
            invoiceType: 'Normal',
            lines: [{
                description: '',
                quantity: 1,
                unitPrice: 100,
                taxRate: 21,
                surchargeRate: 0,
                tipoOperacion: 'Nacional',
            }],
        });
        expect(result.success).toBe(false);
    });
});

describe('leadFormSchema', () => {
    it('acepta lead con nombre', () => {
        const result = leadFormSchema.safeParse({
            name: 'Prospecto',
            email: 'lead@test.com',
            status: 'New',
        });
        expect(result.success).toBe(true);
    });

    it('rechaza nombre vacío', () => {
        const result = leadFormSchema.safeParse({
            name: '',
            status: 'New',
        });
        expect(result.success).toBe(false);
    });

    it('acepta email vacío como string literal', () => {
        const result = leadFormSchema.safeParse({
            name: 'Lead',
            email: '',
            status: 'New',
        });
        expect(result.success).toBe(true);
    });
});

describe('expenseCreateSchema', () => {
    it('acepta gasto con proveedor', () => {
        const result = expenseCreateSchema.safeParse({
            issueDate: '2026-03-01',
            supplierName: 'Proveedor SL',
        });
        expect(result.success).toBe(true);
    });

    it('rechaza sin proveedor ni número de factura', () => {
        const result = expenseCreateSchema.safeParse({
            issueDate: '2026-03-01',
        });
        expect(result.success).toBe(false);
    });
});

describe('salesOrderCreateSchema', () => {
    it('acepta pedido con línea válida', () => {
        const result = salesOrderCreateSchema.safeParse({
            customerId: 'cust-1',
            number: 'SO-001',
            orderDate: '2026-03-01',
            lines: [{ description: 'Producto', quantity: 2, unitPrice: 50, taxRate: 21 }],
        });
        expect(result.success).toBe(true);
    });

    it('rechaza pedido sin cliente', () => {
        const result = salesOrderCreateSchema.safeParse({
            customerId: '',
            number: 'SO-001',
            orderDate: '2026-03-01',
            lines: [{ description: 'Producto', quantity: 1, unitPrice: 10, taxRate: 21 }],
        });
        expect(result.success).toBe(false);
    });
});

describe('createUserSchema', () => {
    it('acepta usuario válido', () => {
        const result = createUserSchema.safeParse({
            firstName: 'Ana',
            email: 'ana@test.com',
        });
        expect(result.success).toBe(true);
    });

    it('rechaza email inválido', () => {
        const result = createUserSchema.safeParse({
            firstName: 'Ana',
            email: 'no-email',
        });
        expect(result.success).toBe(false);
    });
});
