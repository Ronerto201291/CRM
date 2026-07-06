import { serverFetchList } from '@/lib/serverFetch';
import DeferredEntriesClient from './DeferredEntriesClient';

interface DeferredEntry {
    id: string;
    entryType: string;
    description: string;
    totalAmount: number;
    periodStart: string;
    periodEnd: string;
    recognizedAmount: number;
    remainingAmount: number;
    monthlyAmount: number;
    totalMonths: number;
    status: string;
    deferralAccountCode: string;
    counterpartAccountCode: string;
    createdAt: string;
}

export default async function DeferredEntriesPage() {
    const initialEntries = await serverFetchList<DeferredEntry>('deferred-entries?status=Active');
    return <DeferredEntriesClient initialEntries={initialEntries} />;
}
