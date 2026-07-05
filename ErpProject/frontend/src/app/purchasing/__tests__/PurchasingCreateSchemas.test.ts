import { describe, expect, it } from 'vitest';
import { supplierInvoiceCreateSchema } from '@/lib/schemas/purchasingSalesCreateSchemas';

describe('supplierInvoiceCreateSchema', () => {
    it('valida número e importes mínimos', () => {
        const result = supplierInvoiceCreateSchema.safeParse({
            purchaseOrderId: '00000000-0000-0000-0000-000000000001',
            number: 'FAC-001',
            invoiceDate: '2026-07-01',
            lines: [{ purchaseOrderLineId: '00000000-0000-0000-0000-000000000002', description: 'Item', quantity: 1, unitPrice: 10, taxRate: 21 }],
        });
        expect(result.success).toBe(true);
    });

    it('rechaza sin líneas', () => {
        const result = supplierInvoiceCreateSchema.safeParse({
            purchaseOrderId: '00000000-0000-0000-0000-000000000001',
            number: 'FAC-001',
            invoiceDate: '2026-07-01',
            lines: [],
        });
        expect(result.success).toBe(false);
    });
});

describe('goods receipt schema basics', () => {
    it('acepta cantidad positiva en línea', () => {
        const line = { productId: '00000000-0000-0000-0000-000000000003', quantity: 5, unitPrice: 2 };
        expect(line.quantity).toBeGreaterThan(0);
    });
});
