import { serverFetchList } from '@/lib/serverFetch';
import DeliveriesClient from './DeliveriesClient';

interface DeliveryNote {
    id: string;
    number: string;
    deliveryDate: string;
    customerName: string;
    status: string;
    lineCount: number;
    createdAt: string;
}

export default async function SalesDeliveriesPage() {
    const initialDeliveries = await serverFetchList<DeliveryNote>('v1/sales/deliveries');
    return <DeliveriesClient initialDeliveries={initialDeliveries} />;
}
