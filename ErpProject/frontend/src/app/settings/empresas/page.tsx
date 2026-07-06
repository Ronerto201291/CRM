import { cookies } from 'next/headers';
import EmpresasClient from './EmpresasClient';

const backendUrl = () =>
    process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

async function adminFetch<T>(path: string): Promise<{ data: T | null; forbidden: boolean }> {
    const cookieStore = await cookies();
    const token = cookieStore.get('erp_token')?.value;
    const tenantId = cookieStore.get('tenantId')?.value;
    if (!token) return { data: null, forbidden: false };

    const res = await fetch(`${backendUrl()}/api/${path}`, {
        headers: {
            Authorization: `Bearer ${token}`,
            ...(tenantId ? { 'X-Tenant-Id': tenantId } : {}),
        },
        cache: 'no-store',
    });
    if (res.status === 403) return { data: null, forbidden: true };
    if (!res.ok) return { data: null, forbidden: false };
    return { data: await res.json() as T, forbidden: false };
}

export default async function EmpresasPage() {
    const [companiesRes, invitationsRes] = await Promise.all([
        adminFetch<unknown[]>('admin/companies'),
        adminFetch<unknown[]>('admin/invitations'),
    ]);
    const forbidden = companiesRes.forbidden || invitationsRes.forbidden;
    return (
        <EmpresasClient
            initialCompanies={(Array.isArray(companiesRes.data) ? companiesRes.data : []) as never}
            initialInvitations={(Array.isArray(invitationsRes.data) ? invitationsRes.data : []) as never}
            initialForbidden={forbidden}
        />
    );
}
