import { serverFetch, serverFetchList } from '@/lib/serverFetch';
import AlertsClient from './AlertsClient';

interface Alert {
    id: string;
    title: string;
    description?: string;
    scheduledAt: string;
    clientId?: string;
    clientName?: string;
    isAcknowledged: boolean;
    acknowledgedAt?: string;
    snoozedUntil?: string;
    createdAt: string;
}

interface Client {
    id: string;
    name: string;
}

export default async function AlertsPage() {
    const [alertsData, initialClients] = await Promise.all([
        serverFetch<Alert[]>('crm/alerts'),
        serverFetchList<Client>('clients?pageSize=500'),
    ]);
    const initialAlerts = Array.isArray(alertsData) ? alertsData : [];
    return (
        <AlertsClient initialAlerts={initialAlerts} initialClients={initialClients} />
    );
}
