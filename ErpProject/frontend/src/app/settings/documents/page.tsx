import { serverFetchList } from '@/lib/serverFetch';
import DocumentsClient from './DocumentsClient';

export interface DocumentItem {
    id: string;
    fileName: string;
    contentType: string;
    sizeBytes: number;
    entityType?: string | null;
    entityId?: string | null;
    description?: string | null;
    uploadedAt: string;
}

export default async function DocumentsPage() {
    const initialDocuments = await serverFetchList<DocumentItem>('documents');
    return <DocumentsClient initialDocuments={initialDocuments} />;
}
