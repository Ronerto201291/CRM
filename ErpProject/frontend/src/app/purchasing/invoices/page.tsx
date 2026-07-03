import { serverFetchList } from '@/lib/serverFetch';
import PurchasingInvoicesClient from './InvoicesClient';

interface SupplierInvoice {
    id: string;
    number: string;
    invoiceDate: string;
    supplierId?: string;
    supplierName?: string;
    supplierTaxId?: string;
    subtotal: number;
    taxAmount: number;
    total: number;
    status: 'Draft' | 'Approved' | 'Paid';
    createdAt: string;
}

export default async function PurchasingInvoicesPage() {
    const initialInvoices = await serverFetchList<SupplierInvoice>('purchasing/invoices');
    return <PurchasingInvoicesClient initialInvoices={initialInvoices} />;
}
