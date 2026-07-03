import { cookies } from 'next/headers';
import { parseListResponse } from './parseListResponse';

const backendUrl = () =>
    process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

/** Fetch autenticado desde Server Components (ADR-0018 #43). */
export async function serverFetch<T>(path: string): Promise<T | null> {
    const cookieStore = await cookies();
    const token = cookieStore.get('erp_token')?.value;
    const tenantId = cookieStore.get('tenantId')?.value;
    if (!token) return null;

    const normalized = path.startsWith('/') ? path.slice(1) : path;
    const res = await fetch(`${backendUrl()}/api/${normalized}`, {
        headers: {
            Authorization: `Bearer ${token}`,
            ...(tenantId ? { 'X-Tenant-Id': tenantId } : {}),
        },
        cache: 'no-store',
    });

    if (!res.ok) return null;
    return res.json() as Promise<T>;
}

/** Lista paginada o plana desde Server Components. */
export async function serverFetchList<T>(path: string): Promise<T[]> {
    const data = await serverFetch<unknown>(path);
    if (!data) return [];
    return parseListResponse<T>(data);
}
