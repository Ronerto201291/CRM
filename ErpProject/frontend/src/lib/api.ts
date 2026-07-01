const API_BASE = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';

export async function apiFetch(path: string, options: RequestInit = {}) {
    const res = await fetch(`${API_BASE}${path}`, {
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

export async function apiAuthFetch(path: string, token: string, options: RequestInit = {}) {
    return apiFetch(path, {
        ...options,
        headers: {
            Authorization: `Bearer ${token}`,
            ...options.headers,
        },
    });
}
