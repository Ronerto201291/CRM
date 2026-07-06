import { serverFetch } from '@/lib/serverFetch';
import OrderDetailClient, { type PurchaseOrderDetail } from './OrderDetailClient';

export default async function PurchaseOrderDetailPage({ params }: { params: Promise<{ id: string }> }) {
    const { id } = await params;
    const initialOrder = await serverFetch<PurchaseOrderDetail>(`v1/purchasing/orders/${id}`);
    return <OrderDetailClient id={id} initialOrder={initialOrder} />;
}
