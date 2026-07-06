import { z } from 'zod';

const orderLineSchema = z.object({
    description: z.string(),
    quantity: z.number().min(0),
    unitPrice: z.number(),
    taxRate: z.number().min(0).max(100),
    productId: z.string().optional(),
}).superRefine((line, ctx) => {
    if ((line.description.trim() || line.unitPrice > 0) && !line.description.trim()) {
        ctx.addIssue({ code: 'custom', message: 'Descripción obligatoria', path: ['description'] });
    }
});

export const purchaseOrderCreateSchema = z.object({
    supplierId: z.string().min(1, 'Selecciona un proveedor'),
    number: z.string().min(1, 'Número de pedido obligatorio'),
    orderDate: z.string().min(1),
    notes: z.string().optional(),
    lines: z.array(orderLineSchema).min(1),
}).superRefine((data, ctx) => {
    if (data.lines.every((l) => !l.description.trim())) {
        ctx.addIssue({ code: 'custom', message: 'Añade al menos una línea con descripción', path: ['lines'] });
    }
});

export const goodsReceiptCreateSchema = z.object({
    purchaseOrderId: z.string().min(1, 'Selecciona un pedido de compra'),
    number: z.string().min(1, 'Número de recepción obligatorio'),
    receiptDate: z.string().min(1),
    lines: z.array(z.object({
        purchaseOrderLineId: z.string().min(1, 'Línea de pedido obligatoria'),
        productId: z.string().optional(),
        description: z.string().min(1, 'Descripción obligatoria'),
        quantityReceived: z.number().positive('Cantidad debe ser mayor que 0'),
        unitPrice: z.number().min(0, 'Precio no puede ser negativo'),
    })).min(1, 'Añade al menos una línea'),
});

export const deliveryNoteCreateSchema = z.object({
    salesOrderId: z.string().optional(),
    number: z.string().min(1, 'Número de albarán obligatorio'),
    deliveryDate: z.string().min(1),
    lines: z.array(z.object({
        salesOrderLineId: z.string().optional(),
        productId: z.string().optional(),
        description: z.string().min(1, 'Descripción obligatoria'),
        shippedQuantity: z.number().positive('Cantidad debe ser mayor que 0'),
    })).min(1, 'Añade al menos una línea'),
});

export const supplierInvoiceCreateSchema = z.object({
    supplierId: z.string().min(1, 'Selecciona un proveedor'),
    purchaseOrderId: z.string().min(1, 'Selecciona un pedido de compra'),
    number: z.string().min(1, 'Número de factura obligatorio'),
    invoiceDate: z.string().min(1, 'Fecha obligatoria'),
    lines: z.array(z.object({
        purchaseOrderLineId: z.string().min(1, 'Línea de pedido obligatoria'),
        productId: z.string().optional(),
        description: z.string().min(1, 'Descripción obligatoria'),
        quantity: z.number().positive('Cantidad debe ser mayor que 0'),
        unitPrice: z.number().min(0, 'Precio no puede ser negativo'),
        taxRate: z.number().min(0).max(100),
    })).min(1, 'Añade al menos una línea'),
});

export const customerSalesInvoiceCreateSchema = z.object({
    salesOrderId: z.string().min(1, 'Pedido de venta obligatorio'),
    number: z.string().min(1, 'Número interno obligatorio'),
    invoiceDate: z.string().min(1, 'Fecha obligatoria'),
    lines: z.array(z.object({
        salesOrderLineId: z.string().optional(),
        productId: z.string().optional(),
        billedQuantity: z.number().positive('Cantidad debe ser mayor que 0'),
        unitPrice: z.number().min(0, 'Precio no puede ser negativo'),
    })).min(1, 'Añade al menos una línea'),
});

export type PurchaseOrderCreateFormValues = z.infer<typeof purchaseOrderCreateSchema>;
export type GoodsReceiptCreateFormValues = z.infer<typeof goodsReceiptCreateSchema>;
export type DeliveryNoteCreateFormValues = z.infer<typeof deliveryNoteCreateSchema>;
export type SupplierInvoiceCreateFormValues = z.infer<typeof supplierInvoiceCreateSchema>;
export type CustomerSalesInvoiceCreateFormValues = z.infer<typeof customerSalesInvoiceCreateSchema>;
