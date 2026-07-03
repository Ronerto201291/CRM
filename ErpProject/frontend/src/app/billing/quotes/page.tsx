import { serverFetchList } from '@/lib/serverFetch';
import QuotesClient from './QuotesClient';

export default async function QuotesPage() {
    const [initialQuotes, initialClients, initialProspects] = await Promise.all([
        serverFetchList('quotes?pageSize=500'),
        serverFetchList('clients?pageSize=500'),
        serverFetchList('leads?pageSize=500'),
    ]);
    return (
        <QuotesClient
            initialQuotes={initialQuotes as never}
            initialClients={initialClients as never}
            initialProspects={initialProspects as never}
        />
    );
}
