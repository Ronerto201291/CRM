import { renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { useApi } from '@/hooks/useApi';

const mockApiAuthFetch = vi.fn();
const mockApiFetch = vi.fn();

vi.mock('@/context/TenantContext', () => ({
    useTenant: () => ({ tenantId: 'tenant-123', tenantName: 'Test Co' }),
}));

vi.mock('@/lib/api', () => ({
    apiAuthFetch: (...args: unknown[]) => mockApiAuthFetch(...args),
    apiFetch: (...args: unknown[]) => mockApiFetch(...args),
}));

describe('useApi', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        document.cookie = 'erp_token=test-jwt; path=/';
        mockApiAuthFetch.mockResolvedValue({ ok: true });
        mockApiFetch.mockResolvedValue({ public: true });
    });

    it('llama apiAuthFetch con token y tenantId', async () => {
        const { result } = renderHook(() => useApi());

        await result.current.apiCall('/clients');

        expect(mockApiAuthFetch).toHaveBeenCalledWith(
            '/clients',
            'test-jwt',
            'tenant-123',
            {},
        );
    });

    it('usa apiFetch cuando skipAuth es true', async () => {
        const { result } = renderHook(() => useApi());

        await result.current.apiCall('/auth/login', { skipAuth: true, method: 'POST' });

        await waitFor(() => {
            expect(mockApiFetch).toHaveBeenCalledWith('/auth/login', { method: 'POST' });
        });
        expect(mockApiAuthFetch).not.toHaveBeenCalled();
    });
});
