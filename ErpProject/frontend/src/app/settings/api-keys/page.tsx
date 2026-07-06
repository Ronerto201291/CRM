import { serverFetchList } from '@/lib/serverFetch';
import ApiKeysClient from './ApiKeysClient';

interface ApiKey {
    id: string;
    name: string;
    keyPrefix: string;
    createdAt: string;
    lastUsed?: string;
    rateLimit: number;
    isActive: boolean;
}

export default async function ApiKeysPage() {
    const initialApiKeys = await serverFetchList<ApiKey>('api-keys');
    return <ApiKeysClient initialApiKeys={initialApiKeys} />;
}
