import { serverFetch, serverFetchList } from '@/lib/serverFetch';
import { parseListResponse, parseTotalCount } from '@/lib/parseListResponse';
import CrmHomeClient from './CrmHomeClient';

export interface Client { id: string; name: string; email: string; phone: string; taxId: string; address: string; }
export interface Supplier { id: string; name: string; taxId: string; email: string; phone: string; isActive: boolean; }
export interface Contact {
    id: string; name: string; email: string; phone: string; position: string;
    clientId?: string; supplierId?: string; clientName?: string; supplierName?: string;
}

export default async function CrmPage() {
    const [initialClients, initialSuppliers, initialContacts, leadsData] = await Promise.all([
        serverFetchList<Client>('clients?pageSize=500'),
        serverFetchList<Supplier>('suppliers?pageSize=500'),
        serverFetchList<Contact>('contacts?pageSize=500'),
        serverFetch<unknown>('leads?pageSize=500'),
    ]);
    const initialProspectsCount = leadsData ? parseTotalCount(leadsData, parseListResponse(leadsData).length) : 0;

    return (
        <CrmHomeClient
            initialClients={initialClients}
            initialSuppliers={initialSuppliers}
            initialContacts={initialContacts}
            initialProspectsCount={initialProspectsCount}
        />
    );
}
