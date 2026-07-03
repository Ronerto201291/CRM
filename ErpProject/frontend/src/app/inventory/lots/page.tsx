import { serverFetchList } from '@/lib/serverFetch';
import LotsClient from './LotsClient';

interface ProductLot {
    id: string;
    lotNumber: string;
    expirationDate?: string;
    quantity: number;
    unitCost: number;
}

export default async function LotsPage() {
    const initialLots = await serverFetchList<ProductLot>('v1/inventory/lots');
    return <LotsClient initialLots={initialLots} />;
}
