import { serverFetchList } from '@/lib/serverFetch';
import InventoryClient from './InventoryClient';

interface Product {
    id: string;
    name: string;
    sku: string;
    costPrice: number;
    salePrice: number;
    vatPercent: number;
    totalStock: number;
    reorderPoint: number;
    isActive: boolean;
    type: string;
}

interface Warehouse {
    id: string;
    name: string;
}

export default async function InventoryPage() {
    const [initialProducts, initialWarehouses] = await Promise.all([
        serverFetchList<Product>('inventory/products?pageSize=500'),
        serverFetchList<Warehouse>('inventory/warehouses'),
    ]);
    return (
        <InventoryClient
            initialProducts={initialProducts}
            initialWarehouses={initialWarehouses}
        />
    );
}
