import { serverFetchList } from '@/lib/serverFetch';
import ProvisionsClient from './ProvisionsClient';

interface Provision {
    id: string;
    code: string;
    description: string;
    amount: number;
    dueDate: string;
    status: string;
    linkedJournalEntryId?: string;
    createdAt: string;
}

export default async function ProvisionsPage() {
    const initialProvisions = await serverFetchList<Provision>('v1/accounting/provisions?status=Active');
    return (
        <ProvisionsClient
            initialProvisions={initialProvisions}
            initialStatusFilter="Active"
        />
    );
}
