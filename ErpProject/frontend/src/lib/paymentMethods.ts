/** Alineado con PaymentMethods.cs (Billing) — ADR-0018 #42b */
export const PAYMENT_METHODS = [
    { value: 'bank', label: 'Transferencia bancaria', account: '572' },
    { value: 'cash', label: 'Efectivo / caja', account: '570' },
    { value: 'card', label: 'Tarjeta / TPV', account: '5721' },
    { value: 'bizum', label: 'Bizum', account: '5722' },
    { value: 'transfer', label: 'Transferencia (alias)', account: '572' },
] as const;

export type PaymentMethodValue = (typeof PAYMENT_METHODS)[number]['value'];
