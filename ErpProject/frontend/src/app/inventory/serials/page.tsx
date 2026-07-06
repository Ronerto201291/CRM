import { serverFetchList } from '@/lib/serverFetch';
import SerialsClient from './SerialsClient';

interface SerialNumber {
    id: string;
    serial: string;
    productId: string;
    lotId?: string;
    status: string;
    soldDate?: string;
}

interface Product {
    id: string;
    name: string;
    sku?: string;
}

export default async function SerialsPage() {
    const [initialSerials, initialProducts] = await Promise.all([
        serverFetchList<SerialNumber>('v1/inventory/serials'),
        serverFetchList<Product>('v1/inventory/products'),
    ]);
    return <SerialsClient initialSerials={initialSerials} initialProducts={initialProducts} />;
}
