import { serverFetch } from '@/lib/serverFetch';
import RecargoClient from './RecargoClient';

interface RecargoItem {
    invoiceId: string;
    invoiceNumber: string;
    clientTaxId: string;
    clientName: string;
    baseAmount: number;
    surchargeRate: number;
    surchargeAmount: number;
    invoiceDate: string;
}

function currentQuarter(): number {
    return Math.floor(new Date().getMonth() / 3) + 1;
}

export default async function RecargoPage() {
    const year = new Date().getFullYear();
    const quarter = currentQuarter();
    const data = await serverFetch<{ period?: string; recargos?: RecargoItem[] }>(
        `v1/accounting/recargo?year=${year}&q=${quarter}`,
    );
    return (
        <RecargoClient
            initialRecargoList={Array.isArray(data?.recargos) ? data.recargos : []}
            initialPeriod={data?.period ?? ''}
            initialYear={year}
            initialQuarter={quarter}
        />
    );
}
