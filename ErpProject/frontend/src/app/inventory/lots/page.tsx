import { serverFetchList } from '@/lib/serverFetch';
import LotsClient from './LotsClient';

interface ProductLot {
    id: string;
    productId: string;
    lotNumber: string;
    expirationDate?: string;
    quantity: number;
    unitCost: number;
}

interface Product {
    id: string;
    name: string;
    sku?: string;
}

export default async function LotsPage() {
    const [initialLots, initialProducts] = await Promise.all([
        serverFetchList<ProductLot>('v1/inventory/lots'),
        serverFetchList<Product>('v1/inventory/products'),
    ]);
    return <LotsClient initialLots={initialLots} initialProducts={initialProducts} />;
}
