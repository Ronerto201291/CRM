import { serverFetchList } from '@/lib/serverFetch';
import ServicesClient from './ServicesClient';

export interface ServiceCatalogItem {
    id: string;
    name: string;
    description?: string;
    defaultPrice: number;
    defaultTaxRate: number;
    defaultPeriodicity: 'Monthly' | 'Quarterly' | 'Yearly';
    isActive: boolean;
    createdAt: string;
}

export default async function ServicesPage() {
    const initialServices = await serverFetchList<ServiceCatalogItem>('service-catalog?includeInactive=true');
    return <ServicesClient initialServices={initialServices} />;
}
