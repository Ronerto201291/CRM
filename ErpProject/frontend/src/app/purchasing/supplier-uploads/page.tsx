import { serverFetchList } from '@/lib/serverFetch';
import SupplierUploadsClient from './SupplierUploadsClient';

export interface SupplierInvoiceUpload {
    id: string;
    supplierId: string;
    supplierName: string;
    fileName: string;
    status: 'Pending' | 'Reviewed';
    comment?: string | null;
    uploadedAt: string;
}

export default async function SupplierUploadsPage() {
    const initialUploads = await serverFetchList<SupplierInvoiceUpload>('suppliers/uploads');
    return <SupplierUploadsClient initialUploads={initialUploads} />;
}
