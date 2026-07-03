import { serverFetchList } from '@/lib/serverFetch';
import FacturaEClient from './FacturaEClient';

interface Invoice {
    id: string;
    number: string;
    issueDate: string;
    status: string;
    isLocked: boolean;
    verifactuHuella?: string | null;
    clientName?: string;
    total: number;
}

export default async function FacturaEPage() {
    const allInvoices = await serverFetchList<Invoice>('invoices?pageSize=500');
    const lockedInvoices = allInvoices.filter((i) => i.isLocked);
    return <FacturaEClient initialInvoices={lockedInvoices} />;
}
