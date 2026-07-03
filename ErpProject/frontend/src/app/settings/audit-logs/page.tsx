import { serverFetch } from '@/lib/serverFetch';
import AuditLogsClient from './AuditLogsClient';

interface AuditLog {
    id: string;
    entityType: string;
    entityId: string;
    action: string;
    userId: string;
    userName: string;
    changes: string;
    timestamp: string;
    ipAddress?: string;
}

export default async function AuditLogsPage() {
    const dateFrom = new Date();
    dateFrom.setDate(dateFrom.getDate() - 30);
    const initialDateFrom = dateFrom.toISOString().split('T')[0];
    const initialDateTo = new Date().toISOString().split('T')[0];

    const data = await serverFetch<AuditLog[]>(
        `audit-logs?dateFrom=${initialDateFrom}&dateTo=${initialDateTo}`,
    );

    return (
        <AuditLogsClient
            initialLogs={Array.isArray(data) ? data : []}
            initialDateFrom={initialDateFrom}
            initialDateTo={initialDateTo}
        />
    );
}
