import { serverFetch } from '@/lib/serverFetch';
import ExpenseDetailClient from './ExpenseDetailClient';

interface ExpenseDocumentDetail {
    id: string;
    invoiceNumber?: string;
    supplierName?: string;
    supplierTaxId?: string;
    issueDate?: string;
    taxBase?: number;
    vatRate?: number;
    vatAmount?: number;
    irpfRate?: number;
    irpfAmount?: number;
    total?: number;
    status: string;
    isValidated: boolean;
    validatedAt?: string;
    ocrData?: Record<string, unknown>;
    lines: unknown[];
    uploads: unknown[];
}

export default async function ExpenseDetailPage({
    params,
}: {
    params: Promise<{ id: string }>;
}) {
    const { id } = await params;
    const initialDoc = await serverFetch<ExpenseDocumentDetail>(`expenses/${id}`);
    return <ExpenseDetailClient id={id} initialDoc={initialDoc} />;
}
