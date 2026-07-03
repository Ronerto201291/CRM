import { serverFetchList } from '@/lib/serverFetch';
import SalesInvoicesClient from './SalesInvoicesClient';

interface SalesInvoice {
    id: string;
    number: string;
    billingInvoiceNumber?: string;
    invoiceDate: string;
    subTotal: number;
    taxAmount: number;
    total: number;
    lineCount: number;
    createdAt: string;
}

export default async function SalesInvoicesPage() {
    const initialInvoices = await serverFetchList<SalesInvoice>('v1/sales/invoices?pageSize=500');
    return <SalesInvoicesClient initialInvoices={initialInvoices} />;
}
