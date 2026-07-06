import { z } from 'zod';

export const expenseCreateSchema = z.object({
    invoiceNumber: z.string().optional(),
    supplierName: z.string().optional(),
    supplierTaxId: z.string().optional(),
    issueDate: z.string().min(1, 'La fecha es obligatoria'),
    taxBase: z.string().optional(),
    vatRate: z.string().optional(),
    vatAmount: z.string().optional(),
    irpfRate: z.string().optional(),
    irpfAmount: z.string().optional(),
    total: z.string().optional(),
}).refine(
    (data) => Boolean(data.supplierName?.trim() || data.invoiceNumber?.trim()),
    { message: 'Introduce al menos el proveedor o número de factura', path: ['supplierName'] },
);

export type ExpenseCreateFormValues = z.infer<typeof expenseCreateSchema>;
