import { serverFetch, serverFetchList } from '@/lib/serverFetch';
import { buildDashboardStats } from '@/lib/buildDashboardStats';
import DashboardClient from './DashboardClient';

export default async function DashboardPage() {
    const [invoices, expStats, iva] = await Promise.all([
        serverFetchList('invoices?pageSize=500'),
        serverFetch('expenses/stats'),
        serverFetch('accounting/liquidacion-iva'),
    ]);
    const initialData = buildDashboardStats(invoices, expStats, iva);
    return <DashboardClient initialData={initialData} />;
}
