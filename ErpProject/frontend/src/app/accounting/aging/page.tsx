import { serverFetch } from '@/lib/serverFetch';
import AgingClient, { type AgingReport } from './AgingClient';

export default async function AgingPage() {
    const initialReport = await serverFetch<AgingReport>('accounting/aging');
    return <AgingClient initialReport={initialReport} />;
}
