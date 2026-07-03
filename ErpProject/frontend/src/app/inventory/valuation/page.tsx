import { serverFetchList } from '@/lib/serverFetch';
import ValuationClient from './ValuationClient';

interface ValuationProduct {
    id: string;
    name: string;
    quantity: number;
    unitCost: number;
    totalValue: number;
}

export default async function InventoryValuationPage() {
    const initialProducts = await serverFetchList<ValuationProduct>(
        'v1/inventory/valuation?method=PMP',
    );
    return <ValuationClient initialProducts={initialProducts} initialMethod="PMP" />;
}
