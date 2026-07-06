import { serverFetch } from '@/lib/serverFetch';
import OrderDetailClient from './OrderDetailClient';

interface OrderLine {
    id: string;
    productId?: string;
    description: string;
    quantity: number;
    unitPrice: number;
    taxRate: number;
    total: number;
}

interface SalesOrderDetail {
    id: string;
    number: string;
    orderDate: string;
    customerName: string;
    customerTaxId?: string;
    customerEmail?: string;
    status: string;
    subtotal: number;
    taxAmount: number;
    total: number;
    notes?: string;
    lines: OrderLine[];
}

export default async function SalesOrderDetailPage({
    params,
}: {
    params: Promise<{ id: string }>;
}) {
    const { id } = await params;
    const initialOrder = await serverFetch<SalesOrderDetail>(`v1/sales/orders/${id}`);
    return <OrderDetailClient id={id} initialOrder={initialOrder} />;
}
