'use client';
import { useCallback } from 'react';
import { useTenant } from '@/context/TenantContext';

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

        const headers: Record<string, string> = {
            'Content-Type': 'application/json',
            ...(fetchOptions.headers as Record<string, string>),
        };

        // Agregar Authorization si no se salta
        if (!skipAuth) {
            const token = document.cookie
                .split(';')
                .find(c => c.trim().startsWith('erp_token='))
                ?.split('=')[1];

            if (token) {
                headers['Authorization'] = `Bearer ${token}`;
            }

            // Agregar X-Tenant-Id si existe
            if (tenantId) {
                headers['X-Tenant-Id'] = tenantId;
            }
        }

        const apiUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';
        const fullPath = path.startsWith('/api/') ? path : `/api/${path}`;

        try {
            const response = await fetch(`${apiUrl}${fullPath}`, {
                ...fetchOptions,
                headers,
            });

            // Si es 401, limpiar auth y redirigir
            if (response.status === 401) {
                document.cookie = 'erp_token=; max-age=0; path=/;';
                document.cookie = 'tenantId=; max-age=0; path=/;';
                document.cookie = 'tenantName=; max-age=0; path=/;';
                if (typeof window !== 'undefined') {
                    window.location.href = '/admin';
                }
                return null;
            }

            // Si hay error, lanzar
            if (!response.ok) {
                const errorData = await response.json().catch(() => ({ message: response.statusText }));
                throw new Error(errorData.error || errorData.message || `HTTP ${response.status}`);
            }

            return await response.json();
        } catch (error) {
            console.error(`API Error (${path}):`, error);
            throw error;
        }
    }, [tenantId]);

    return { apiCall };
}
