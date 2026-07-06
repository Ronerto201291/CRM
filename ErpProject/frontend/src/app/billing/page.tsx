import { serverFetchList } from '@/lib/serverFetch';
import BillingClient from './BillingClient';

interface Invoice {
    id: string;
    number: string;
    clientName?: string;
    issueDate: string;
    subtotal: number;
    taxAmount: number;
    irpfAmount: number;
    total: number;
    status: string;
    isLocked: boolean;
    series: string;
}

interface Client {
    id: string;
    name: string;
    taxId: string;
}

export default async function BillingPage() {
    const [initialInvoices, initialClients] = await Promise.all([
        serverFetchList<Invoice>('invoices?pageSize=500'),
        serverFetchList<Client>('clients?pageSize=500'),
    ]);
    return (
        <BillingClient
            initialInvoices={initialInvoices}
            initialClients={initialClients}
        />
    );
}
