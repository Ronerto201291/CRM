'use client';
import { useCallback } from 'react';
import { useTenant } from '@/context/TenantContext';
import { apiAuthFetch } from '@/lib/api';

interface ApiOptions extends RequestInit {
    skipAuth?: boolean;
}

export function useApi() {
    const { tenantId } = useTenant();

    const apiCall = useCallback(async (
        path: string,
        options: ApiOptions = {}
    ) => {
        const { skipAuth = false, ...fetchOptions } = options;

        if (skipAuth) {
            const { apiFetch } = await import('@/lib/api');
            return apiFetch(path, fetchOptions);
        }

        const token = document.cookie
            .split(';')
            .find(c => c.trim().startsWith('erp_token='))
            ?.split('=')[1];

        return apiAuthFetch(path, token, tenantId, fetchOptions);
    }, [tenantId]);

    return { apiCall };
}
