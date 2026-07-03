import { serverFetchList } from '@/lib/serverFetch';
import LeadsClient from './LeadsClient';

export interface Lead {
    id: string;
    name: string;
    email: string;
    phone: string;
    status: string;
    source: string;
    notes: string;
    createdAt: string;
}

export default async function LeadsPage() {
    const initialLeads = await serverFetchList<Lead>('leads?pageSize=500');
    return <LeadsClient initialLeads={initialLeads} />;
}
