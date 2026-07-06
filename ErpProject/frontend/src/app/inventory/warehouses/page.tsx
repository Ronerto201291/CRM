import { serverFetch } from '@/lib/serverFetch';
import WarehousesClient, { type Warehouse } from './WarehousesClient';

export default async function WarehousesPage() {
    const initialWarehouses = (await serverFetch<Warehouse[]>('inventory/warehouses')) ?? [];
    return <WarehousesClient initialWarehouses={initialWarehouses} />;
}
