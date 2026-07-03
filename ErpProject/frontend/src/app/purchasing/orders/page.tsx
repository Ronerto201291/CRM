import { serverFetchList } from '@/lib/serverFetch';
import PurchasingOrdersClient, { type PurchaseOrder } from './PurchasingOrdersClient';

export default async function PurchasingOrdersPage() {
    const initialOrders = await serverFetchList<PurchaseOrder>('purchasing/orders');
    return <PurchasingOrdersClient initialOrders={initialOrders} />;
}
