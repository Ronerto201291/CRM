import { serverFetchList } from '@/lib/serverFetch';
import CreditNotesClient from './CreditNotesClient';

interface Invoice {
    id: string;
    number: string;
    clientName?: string;
    issueDate: string;
    total: number;
    status: string;
    isLocked: boolean;
}

export default async function CreditNotesPage() {
    const [allInvoices, creditNotes] = await Promise.all([
        serverFetchList<Invoice>('invoices?pageSize=500'),
        serverFetchList<Invoice>('invoices?type=CreditNote&pageSize=500'),
    ]);
    const lockedInvoices = allInvoices.filter(
        (i) => i.isLocked || i.status === 'Locked',
    );
    return (
        <CreditNotesClient
            initialInvoices={lockedInvoices}
            initialCreditNotes={creditNotes}
        />
    );
}
