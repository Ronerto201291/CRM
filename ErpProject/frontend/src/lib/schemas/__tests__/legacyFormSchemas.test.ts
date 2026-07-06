import { describe, expect, it } from 'vitest';
import {
    supplierCreateSchema,
    creditNoteCreateSchema,
    guaranteeCreateSchema,
    currencyCreateSchema,
} from '@/lib/schemas/legacyFormSchemas';

describe('legacyFormSchemas', () => {
    it('supplierCreateSchema rechaza nombre vacío', () => {
        const r = supplierCreateSchema.safeParse({ name: '', taxId: 'B12345678' });
        expect(r.success).toBe(false);
    });

    it('creditNoteCreateSchema exige factura y motivo', () => {
        expect(creditNoteCreateSchema.safeParse({ invoiceId: '', reason: '' }).success).toBe(false);
        expect(creditNoteCreateSchema.safeParse({ invoiceId: 'abc', reason: 'Error' }).success).toBe(true);
    });

    it('guaranteeCreateSchema valida importe positivo', () => {
        expect(guaranteeCreateSchema.safeParse({
            type: 'Aval', beneficiary: 'Banco', amount: '0', currency: 'EUR',
        }).success).toBe(false);
        expect(guaranteeCreateSchema.safeParse({
            type: 'Aval', beneficiary: 'Banco', amount: '1000', currency: 'EUR',
        }).success).toBe(true);
    });

    it('currencyCreateSchema exige código ISO 3 letras', () => {
        expect(currencyCreateSchema.safeParse({
            code: 'US', name: 'Dólar', exchangeRateToEur: '1.1',
        }).success).toBe(false);
        expect(currencyCreateSchema.safeParse({
            code: 'USD', name: 'Dólar', exchangeRateToEur: '1.1',
        }).success).toBe(true);
    });
});
