import { serverFetchList } from '@/lib/serverFetch';
import ConsolidationClient from './ConsolidationClient';

interface Subsidiary {
    id: string;
    name: string;
    participationPct: number;
    currency: string;
    balanceSheet?: { totalAssets: number; totalLiabilities: number; netWorth: number };
}

interface ConsolidationGroup {
    id: string;
    name: string;
    currency: string;
    totalAssets: number;
    subsidiaries: Subsidiary[];
}

export default async function ConsolidationPage() {
    const initialGroups = await serverFetchList<ConsolidationGroup>('v1/treasury/consolidation');
    return <ConsolidationClient initialGroups={initialGroups} />;
}
