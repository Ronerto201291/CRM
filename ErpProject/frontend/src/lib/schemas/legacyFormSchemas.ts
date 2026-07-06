import { z } from 'zod';

export const supplierCreateSchema = z.object({
    name: z.string().min(1, 'El nombre es obligatorio'),
    taxId: z.string().min(1, 'El CIF/NIF es obligatorio'),
    email: z.string().email('Email inválido').optional().or(z.literal('')),
    phone: z.string().optional(),
    address: z.string().optional(),
    bankAccount: z.string().optional(),
});

export type SupplierCreateFormValues = z.infer<typeof supplierCreateSchema>;

export const creditNoteCreateSchema = z.object({
    invoiceId: z.string().min(1, 'Selecciona una factura'),
    reason: z.string().min(1, 'El motivo es obligatorio'),
    amount: z.string().optional(),
});

export type CreditNoteCreateFormValues = z.infer<typeof creditNoteCreateSchema>;

export const guaranteeCreateSchema = z.object({
    type: z.string().min(1, 'El tipo es obligatorio'),
    beneficiary: z.string().min(1, 'El beneficiario es obligatorio'),
    amount: z.string().min(1, 'Importe obligatorio').refine(
        v => !Number.isNaN(parseFloat(v)) && parseFloat(v) > 0,
        'Importe debe ser mayor que 0',
    ),
    currency: z.string().min(3).max(3).default('EUR'),
    startDate: z.string().optional(),
    endDate: z.string().optional(),
    autoRenew: z.boolean().optional(),
    bank: z.string().optional(),
    documentRef: z.string().optional(),
});

export type GuaranteeCreateFormValues = z.infer<typeof guaranteeCreateSchema>;

export const currencyCreateSchema = z.object({
    code: z.string().min(3, 'Código ISO obligatorio (3 letras)').max(3),
    name: z.string().min(1, 'El nombre es obligatorio'),
    exchangeRateToEur: z.string().min(1, 'Tipo de cambio obligatorio').refine(
        v => !Number.isNaN(parseFloat(v)) && parseFloat(v) > 0,
        'Tipo de cambio debe ser mayor que 0',
    ),
});

export type CurrencyCreateFormValues = z.infer<typeof currencyCreateSchema>;

export const currencyRateSchema = z.object({
    fromCurrency: z.string().min(3, 'Moneda origen obligatoria').max(3),
    toCurrency: z.string().min(3, 'Moneda destino obligatoria').max(3),
    rate: z.string().min(1, 'Tipo de cambio obligatorio').refine(
        v => !Number.isNaN(parseFloat(v)) && parseFloat(v) > 0,
        'Tipo de cambio debe ser mayor que 0',
    ),
    effectiveDate: z.string().min(1, 'Fecha obligatoria'),
});

export type CurrencyRateFormValues = z.infer<typeof currencyRateSchema>;

export const treasuryBankAccountSchema = z.object({
    name: z.string().min(1, 'Nombre obligatorio'),
    iban: z.string().min(15, 'IBAN obligatorio'),
    bic: z.string().optional(),
    bankName: z.string().optional(),
    notes: z.string().optional(),
    accountingAccountCode: z.string().optional(),
});

export const treasuryCashEffectSchema = z.object({
    clientName: z.string().min(1, 'Cliente obligatorio'),
    clientTaxId: z.string().min(1, 'NIF/CIF obligatorio'),
    effectNumber: z.string().min(1, 'Número de efecto obligatorio'),
    issueDate: z.string().min(1, 'Fecha emisión obligatoria'),
    dueDate: z.string().min(1, 'Fecha vencimiento obligatoria'),
    amount: z.string().min(1, 'Importe obligatorio').refine(
        v => !Number.isNaN(parseFloat(v)) && parseFloat(v) > 0,
        'Importe debe ser mayor que 0',
    ),
    bankAccountId: z.string().optional(),
});

export const treasuryPaymentOrderSchema = z.object({
    paymentType: z.string().min(1, 'Tipo obligatorio'),
    beneficiaryName: z.string().min(1, 'Beneficiario obligatorio'),
    beneficiaryTaxId: z.string().optional(),
    beneficiaryIban: z.string().min(15, 'IBAN obligatorio'),
    description: z.string().optional(),
    amount: z.string().min(1, 'Importe obligatorio').refine(
        v => !Number.isNaN(parseFloat(v)) && parseFloat(v) > 0,
        'Importe debe ser mayor que 0',
    ),
    scheduledDate: z.string().optional(),
    bankAccountId: z.string().optional(),
});

export const treasuryPosTerminalSchema = z.object({
    name: z.string().min(1, 'Nombre obligatorio'),
    terminalCode: z.string().min(1, 'Código terminal obligatorio'),
});

export const treasuryPosPaymentSchema = z.object({
    terminalId: z.string().min(1, 'Terminal obligatorio'),
    invoiceId: z.string().min(1, 'Factura obligatoria'),
    amount: z.string().min(1, 'Importe obligatorio').refine(
        v => !Number.isNaN(parseFloat(v)) && parseFloat(v) > 0,
        'Importe debe ser mayor que 0',
    ),
});

export const treasuryCashSessionOpenSchema = z.object({
    openingBalance: z.string().min(1, 'Saldo apertura obligatorio').refine(
        v => !Number.isNaN(parseFloat(v)) && parseFloat(v) >= 0,
        'Saldo debe ser numérico',
    ),
});

export const treasuryCashSessionCloseSchema = z.object({
    countedClosingBalance: z.string().min(1, 'Saldo contado obligatorio').refine(
        v => !Number.isNaN(parseFloat(v)) && parseFloat(v) >= 0,
        'Saldo debe ser numérico',
    ),
});

export type TreasuryBankAccountFormValues = z.infer<typeof treasuryBankAccountSchema>;
export type TreasuryCashEffectFormValues = z.infer<typeof treasuryCashEffectSchema>;
export type TreasuryPaymentOrderFormValues = z.infer<typeof treasuryPaymentOrderSchema>;
