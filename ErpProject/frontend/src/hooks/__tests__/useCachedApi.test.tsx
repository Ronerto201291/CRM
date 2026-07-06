import { renderHook } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { useCachedApi } from '@/hooks/useCachedApi';
import { invalidateApiCache, setApiCache } from '@/lib/apiCache';

const mockApiCall = vi.fn();

vi.mock('@/context/TenantContext', () => ({
    useTenant: () => ({ tenantId: 'tenant-abc', tenantName: 'Cached Co' }),
}));

vi.mock('@/hooks/useApi', () => ({
    useApi: () => ({ apiCall: mockApiCall }),
}));

describe('useCachedApi', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        invalidateApiCache();
        mockApiCall.mockResolvedValue({ items: [1, 2] });
    });

    it('devuelve datos cacheados sin repetir petición', async () => {
        setApiCache('api:tenant-abc:/suppliers', { cached: true }, 60_000);

        const { result } = renderHook(() => useCachedApi());
        const data = await result.current.fetchCached<{ cached: boolean }>('/suppliers');

        expect(data).toEqual({ cached: true });
        expect(mockApiCall).not.toHaveBeenCalled();
    });

    it('llama apiCall y guarda en caché cuando no hay hit', async () => {
        const { result } = renderHook(() => useCachedApi());
        const data = await result.current.fetchCached<{ items: number[] }>('/clients');

        expect(data).toEqual({ items: [1, 2] });
        expect(mockApiCall).toHaveBeenCalledWith('/clients');

        mockApiCall.mockClear();
        const cached = await result.current.fetchCached<{ items: number[] }>('/clients');
        expect(cached).toEqual({ items: [1, 2] });
        expect(mockApiCall).not.toHaveBeenCalled();
    });

    it('invalidateCached elimina entradas del tenant', async () => {
        setApiCache('api:tenant-abc:/invoices', { stale: true }, 60_000);

        const { result } = renderHook(() => useCachedApi());
        result.current.invalidateCached('/invoices');

        const data = await result.current.fetchCached<{ items: number[] }>('/invoices');
        expect(data).toEqual({ items: [1, 2] });
        expect(mockApiCall).toHaveBeenCalledWith('/invoices');
    });
});
