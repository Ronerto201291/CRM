import { describe, expect, it } from 'vitest';
import {
    payrollEmployeeSchema,
    payrollManualLineSchema,
    payrollModel111ExportSchema,
    payrollTemplateSchema,
} from '@/lib/schemas/payrollFormSchemas';
import { supplierInvoiceCreateSchema } from '@/lib/schemas/purchasingSalesCreateSchemas';
import { ispCreateSchema, recargoCreateSchema } from '@/lib/schemas/accountingLegacyFormSchemas';
import { siiSubmitSchema, companySettingsSchema } from '@/lib/schemas/settingsFiscalFormSchemas';
import { treasuryBankAccountSchema } from '@/lib/schemas/legacyFormSchemas';

describe('payrollFormSchemas', () => {
    it('payrollEmployeeSchema exige NIF y nombre', () => {
        expect(payrollEmployeeSchema.safeParse({ taxId: '', fullName: 'Ana' }).success).toBe(false);
        expect(payrollEmployeeSchema.safeParse({ taxId: '12345678Z', fullName: 'Ana' }).success).toBe(true);
    });

    it('payrollModel111ExportSchema valida trimestre', () => {
        expect(payrollModel111ExportSchema.safeParse({ year: 2026, quarter: 5 }).success).toBe(false);
        expect(payrollModel111ExportSchema.safeParse({ year: 2026, quarter: 2 }).success).toBe(true);
    });

    it('payrollManualLineSchema exige liquidación', () => {
        expect(payrollManualLineSchema.safeParse({
            settlementId: '', employeeId: 'x', gross: '100', baseCc: '100',
            empSs: '10', erSs: '30', irpfBase: '100', irpfRate: '15', irpfW: '15', net: '85',
        }).success).toBe(false);
    });

    it('payrollTemplateSchema valida porcentajes', () => {
        expect(payrollTemplateSchema.safeParse({
            name: 'Std', empSs: '6', erSs: '30', irpf: 'abc',
        }).success).toBe(false);
    });
});

describe('supplierInvoiceCreateSchema', () => {
    it('exige supplierId y purchaseOrderId', () => {
        expect(supplierInvoiceCreateSchema.safeParse({
            supplierId: '',
            purchaseOrderId: 'po-1',
            number: 'FV-1',
            invoiceDate: '2026-01-01',
            lines: [{ purchaseOrderLineId: 'l1', description: 'x', quantity: 1, unitPrice: 10, taxRate: 21 }],
        }).success).toBe(false);
    });
});

describe('accountingLegacyFormSchemas', () => {
    it('ispCreateSchema valida país', () => {
        expect(ispCreateSchema.safeParse({ supplierCountryCode: 'D', vatableBase: 100, vatRate: 21 }).success).toBe(false);
    });

    it('recargoCreateSchema exige NIF proveedor', () => {
        expect(recargoCreateSchema.safeParse({
            supplierVat: '', supplierIsRE: false, baseAmount: 1000, rechargeRate: 5.2,
        }).success).toBe(false);
    });
});

describe('settingsFiscalFormSchemas', () => {
    it('siiSubmitSchema valida mes', () => {
        expect(siiSubmitSchema.safeParse({ type: 'emitidas', year: 2026, month: 13 }).success).toBe(false);
    });

    it('companySettingsSchema exige nombre y taxId', () => {
        expect(companySettingsSchema.safeParse({ name: 'Co', taxId: 'B12345674' }).success).toBe(true);
    });
});

describe('treasuryBankAccountSchema', () => {
    it('exige IBAN mínimo', () => {
        expect(treasuryBankAccountSchema.safeParse({ name: 'Cuenta', iban: 'ES12' }).success).toBe(false);
    });
});
