/** Alineado con SalesOrder.Status en backend (Open, PartiallyDelivered, Completed) */
export const SALES_ORDER_STATUS_MAP: Record<string, { label: string; cls: string }> = {
    Open: { label: 'Abierto', cls: 'badge-info' },
    PartiallyDelivered: { label: 'Entrega parcial', cls: 'badge-warning' },
    Completed: { label: 'Completado', cls: 'badge-success' },
    Cancelled: { label: 'Cancelado', cls: 'badge-gray' },
};

export const SALES_ORDER_STATUS_FILTERS = ['all', 'Open', 'PartiallyDelivered', 'Completed', 'Cancelled'] as const;
