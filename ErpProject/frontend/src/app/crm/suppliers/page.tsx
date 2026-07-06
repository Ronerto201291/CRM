import { serverFetchList } from '@/lib/serverFetch';
import SuppliersClient, { type Supplier } from './SuppliersClient';

export default async function SuppliersPage() {
    const initialSuppliers = await serverFetchList<Supplier>('suppliers?pageSize=500');
    return <SuppliersClient initialSuppliers={initialSuppliers} />;
}
