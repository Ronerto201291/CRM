import { serverFetchList } from '@/lib/serverFetch';
import CurrenciesClient from './CurrenciesClient';

interface Currency {
    id: string;
    code: string;
    name: string;
    exchangeRateToEur: number;
    trend: 'up' | 'down' | 'stable';
    lastUpdated: string;
    isActive: boolean;
}

export default async function CurrenciesPage() {
    const initialCurrencies = await serverFetchList<Currency>('v1/treasury/currencies');
    return <CurrenciesClient initialCurrencies={initialCurrencies} />;
}
