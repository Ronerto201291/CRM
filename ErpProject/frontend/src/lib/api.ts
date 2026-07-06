const API_BASE = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8081';

function resolveUrl(path: string): string {
    if (path.startsWith('http')) return path;
    const normalized = path.startsWith('/api/') ? path : `/api/${path}`;
    return `${API_BASE}${normalized}`;
}

export function getApiBaseUrl(): string {
    return API_BASE;
}

export async function apiFetch(path: string, options: RequestInit = {}) {
    const res = await fetch(resolveUrl(path), {
        ...options,
        headers: {
            'Content-Type': 'application/json',
            ...options.headers,
        },
    });
    if (!res.ok) {
        const error = await res.json().catch(() => ({ message: res.statusText }));
        throw new Error(error.message || error.error || 'Error de servidor');
    }
    return res.json();
}

export async function apiAuthFetch(
    path: string,
    token: string | undefined,
    tenantId: string | null | undefined,
    options: RequestInit = {},
) {
    const headers: Record<string, string> = {
        'Content-Type': 'application/json',
        ...(options.headers as Record<string, string>),
    };
    if (token) headers.Authorization = `Bearer ${token}`;
    if (tenantId) headers['X-Tenant-Id'] = tenantId;

    const res = await fetch(resolveUrl(path), { ...options, headers });

    if (res.status === 401) {
        document.cookie = 'erp_token=; max-age=0; path=/;';
        document.cookie = 'tenantId=; max-age=0; path=/;';
        document.cookie = 'tenantName=; max-age=0; path=/;';
        if (typeof window !== 'undefined') {
            window.location.href = '/admin';
        }
        return null;
    }

    if (!res.ok) {
        const error = await res.json().catch(() => ({ message: res.statusText }));
        throw new Error(error.message || error.error || `HTTP ${res.status}`);
    }

    return res.json();
}
