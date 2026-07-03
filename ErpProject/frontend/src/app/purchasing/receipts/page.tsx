import { serverFetchList } from '@/lib/serverFetch';
import ReceiptsClient from './ReceiptsClient';

export interface GoodsReceipt {
    id: string;
    number: string;
    receiptDate: string;
    purchaseOrderId?: string;
    supplierName?: string;
    status: 'Draft' | 'Completed';
    lineCount: number;
    createdAt: string;
}

export default async function PurchasingReceiptsPage() {
    const initialReceipts = await serverFetchList<GoodsReceipt>('purchasing/receipts');
    return <ReceiptsClient initialReceipts={initialReceipts} />;
}
