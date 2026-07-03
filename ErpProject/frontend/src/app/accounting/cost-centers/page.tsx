import { serverFetchList } from '@/lib/serverFetch';
import CostCentersClient from './CostCentersClient';

interface CostCenter {
    id: string;
    code: string;
    name: string;
    type: string;
    totalCosts: number;
}

export default async function CostCentersPage() {
    const initialCenters = await serverFetchList<CostCenter>('v1/accounting/cost-centers');
    return <CostCentersClient initialCenters={initialCenters} />;
}
