import { serverFetchList } from '@/lib/serverFetch';
import ProspectsClient from './ProspectsClient';

export interface Prospect {
    id: string;
    name: string;
    email: string;
    phone: string;
    taxId: string;
    address: string;
    status: string;
    source: string;
    notes: string;
    convertedToClientId: string | null;
    createdAt: string;
}

export default async function ProspectsPage() {
    const initialProspects = await serverFetchList<Prospect>('leads?pageSize=500');
    return <ProspectsClient initialProspects={initialProspects} />;
}
