import { serverFetchList } from '@/lib/serverFetch';
import SerialsClient from './SerialsClient';

interface SerialNumber {
    id: string;
    serial: string;
    productId: string;
    status: string;
    soldDate?: string;
}

export default async function SerialsPage() {
    const initialSerials = await serverFetchList<SerialNumber>('v1/inventory/serials');
    return <SerialsClient initialSerials={initialSerials} />;
}
