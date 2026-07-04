import { serverFetch, serverFetchList } from '@/lib/serverFetch';
import ClientEditorClient, { type Client } from './ClientEditorClient';
import type { ServiceCatalogItem } from '../../services/page';

export interface ClientContractedServiceItem {
    id: string;
    clientId: string;
    serviceCatalogItemId: string;
    serviceName: string;
    price: number;
    taxRate: number;
    periodicity: 'Monthly' | 'Quarterly' | 'Yearly';
    startDate: string;
    nextBillingDate: string;
    endDate?: string;
    status: 'Active' | 'Cancelled';
    lastInvoiceId?: string;
}

export default async function ClientEditorPage({ params }: { params: Promise<{ id: string }> }) {
    const { id } = await params;
    const isNew = id === 'new';
    const initialClient = isNew ? null : await serverFetch<Client>(`clients/${id}`);
    const initialContractedServices = isNew ? [] : await serverFetchList<ClientContractedServiceItem>(`clients/${id}/contracted-services`);
    const serviceCatalog = isNew ? [] : await serverFetchList<ServiceCatalogItem>('service-catalog');

    return (
        <ClientEditorClient
            clientId={id}
            initialClient={initialClient}
            initialContractedServices={initialContractedServices}
            serviceCatalog={serviceCatalog}
        />
    );
}
