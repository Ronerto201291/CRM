/** Caché en memoria compartida para peticiones API del cliente (ADR-0018 #49). */
interface CacheEntry {
    data: unknown;
    expires: number;
}

const store = new Map<string, CacheEntry>();

export function getApiCache<T>(key: string): T | undefined {
    const entry = store.get(key);
    if (!entry) return undefined;
    if (Date.now() > entry.expires) {
        store.delete(key);
        return undefined;
    }
    return entry.data as T;
}

export function setApiCache(key: string, data: unknown, ttlMs: number): void {
    store.set(key, { data, expires: Date.now() + ttlMs });
}

export function invalidateApiCache(tenantId?: string | null, pathPrefix?: string): void {
    const prefix = tenantId ? `api:${tenantId}:` : 'api:';
    const fullPrefix = pathPrefix ? `${prefix}${pathPrefix}` : prefix;
    for (const key of store.keys()) {
        if (key.startsWith(fullPrefix)) store.delete(key);
    }
}
