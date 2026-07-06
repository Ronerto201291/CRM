import { serverFetchList } from '@/lib/serverFetch';
import SalesOrdersClient, { type SalesOrder } from './SalesOrdersClient';

export default async function SalesOrdersPage() {
    const initialOrders = await serverFetchList<SalesOrder>('v1/sales/orders');
    return <SalesOrdersClient initialOrders={initialOrders} />;
}
