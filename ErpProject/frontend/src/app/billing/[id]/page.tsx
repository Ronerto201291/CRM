import { serverFetch } from '@/lib/serverFetch';
import InvoiceDetailClient from './InvoiceDetailClient';

interface Invoice {
    id: string;
    number: string;
    series: string;
    fiscalYear: number;
    invoiceType: string;
    issueDate: string;
    dueDate: string;
    subtotal: number;
    taxAmount: number;
    irpfRate: number;
    irpfAmount: number;
    surchargeAmount: number;
    total: number;
    status: string;
    isLocked: boolean;
    clientName?: string;
    invoiceLines: unknown[];
}

export default async function InvoiceDetailPage({
    params,
}: {
    params: Promise<{ id: string }>;
}) {
    const { id } = await params;
    const initialInvoice = await serverFetch<Invoice>(`invoices/${id}`);
    return <InvoiceDetailClient id={id} initialInvoice={initialInvoice} />;
}
