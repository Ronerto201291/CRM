import { serverFetchList } from '@/lib/serverFetch';
import ClientsListClient, { type Client } from './ClientsListClient';

export default async function ClientsPage() {
    const initialClients = await serverFetchList<Client>('clients?pageSize=500');
    return <ClientsListClient initialClients={initialClients} />;
}
