'use client';

import { useCallback } from 'react';
import { useTenant } from '@/context/TenantContext';
import { useApi } from '@/hooks/useApi';
import { getApiCache, setApiCache, invalidateApiCache } from '@/lib/apiCache';

const DEFAULT_TTL_MS = 60_000;

/** Piloto ADR-0018 #49: envuelve useApi con caché TTL en memoria por tenant+path. */
export function useCachedApi() {
    const { tenantId } = useTenant();
    const { apiCall } = useApi();

    const cacheKey = useCallback(
        (path: string) => `api:${tenantId ?? 'none'}:${path}`,
        [tenantId],
    );

    const fetchCached = useCallback(
        async <T>(path: string, ttlMs = DEFAULT_TTL_MS): Promise<T | null> => {
            const key = cacheKey(path);
            const hit = getApiCache<T>(key);
            if (hit !== undefined) return hit;

            const data = (await apiCall(path)) as T | null;
            if (data !== null) setApiCache(key, data, ttlMs);
            return data;
        },
        [apiCall, cacheKey],
    );

    const invalidateCached = useCallback(
        (pathPrefix?: string) => invalidateApiCache(tenantId, pathPrefix),
        [tenantId],
    );

    return { fetchCached, invalidateCached };
}
