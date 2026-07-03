import { serverFetch } from '@/lib/serverFetch';
import ClientEditorClient, { type Client } from './ClientEditorClient';

export default async function ClientEditorPage({ params }: { params: Promise<{ id: string }> }) {
    const { id } = await params;
    const initialClient = id !== 'new' ? await serverFetch<Client>(`clients/${id}`) : null;
    return <ClientEditorClient clientId={id} initialClient={initialClient} />;
}
