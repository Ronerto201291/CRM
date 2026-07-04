import { serverFetch } from '@/lib/serverFetch';
import OrderDetailClient from './OrderDetailClient';

export interface OrderLine {
    id: string;
    productId?: string;
    description: string;
    quantity: number;
    deliveredQuantity: number;
    billedQuantity: number;
    unitPrice: number;
    taxRate: number;
    total: number;
}

export interface PurchaseOrderDetail {
    id: string;
    number: string;
    orderDate: string;
    supplierName: string;
    supplierTaxId?: string;
    supplierEmail?: string;
    status: string;
    subtotal: number;
    taxAmount: number;
    total: number;
    notes?: string;
    lines: OrderLine[];
}

export default async function PurchaseOrderDetailPage({ params }: { params: Promise<{ id: string }> }) {
    const { id } = await params;
    const initialOrder = await serverFetch<PurchaseOrderDetail>(`v1/purchasing/orders/${id}`);
    return <OrderDetailClient id={id} initialOrder={initialOrder} />;
}
