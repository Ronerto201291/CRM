import { serverFetch } from '@/lib/serverFetch';
import FiscalClient, { type FiscalEvent } from './FiscalClient';

export default async function FiscalPage() {
    const initialYear = new Date().getFullYear();
    const data = await serverFetch<FiscalEvent[]>(`fiscal/calendar?year=${initialYear}`);
    const initialEvents = Array.isArray(data) ? data : [];
    return <FiscalClient initialEvents={initialEvents} initialYear={initialYear} />;
}
