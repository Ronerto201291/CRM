import { serverFetchList } from '@/lib/serverFetch';
import CierreClient from './CierreClient';

export interface FiscalPeriod {
    id: string;
    fiscalYear: number;
    closedAt: string;
    resultadoNeto: number;
    closingJournalEntryId: string;
    notes: string;
}

export default async function CierreContablePage() {
    const initialPeriods = await serverFetchList<FiscalPeriod>('accounting/cierre');
    return <CierreClient initialPeriods={initialPeriods} />;
}
