import { serverFetchList } from '@/lib/serverFetch';
import GuaranteesClient from './GuaranteesClient';

export default async function GuaranteesPage() {
    const [initialGuarantees, initialCollaterals] = await Promise.all([
        serverFetchList('v1/treasury/guarantees'),
        serverFetchList('v1/treasury/guarantees/collateral'),
    ]);
    return (
        <GuaranteesClient
            initialGuarantees={initialGuarantees as never}
            initialCollaterals={initialCollaterals as never}
        />
    );
}
