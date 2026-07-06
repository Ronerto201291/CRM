import { cookies } from 'next/headers';
import { NextRequest, NextResponse } from 'next/server';

// Rutas que no requieren token ni tenantId (login, registro, etc.)
const PUBLIC_PATHS = ['auth/login', 'auth/register', 'auth/refresh', 'auth/2fa', 'v1/public'];

// Prefijos permitidos para requests autenticadas — cualquier otra ruta se rechaza.
const ALLOWED_PATH_PREFIXES = [
    'admin/',
    'audit-logs',
    'auth/',
    'automation/',
    'accounting/',
    'clients',
    'company',
    'contacts',
    'crm/',
    'deferred-entries',
    'documents',
    'expenses',
    'fiscal/',
    'inventory/',
    'invoices',
    'leads',
    'notifications/',
    'onboarding/',
    'gestoria/',
    'payroll/',
    'permissions',
    'purchasing/',
    'quotes',
    'reports/',
    'service-catalog',
    'sii/',
    'subscription/',
    'suppliers',
    'tax/',
    'tenant/modules',
    'treasury/',
    'users',
    'v1/accounting/',
    'v1/billing/',
    'v1/inventory/',
    'v1/purchasing/',
    'v1/sales/',
    'v1/treasury/',
];

function isPathAllowed(path: string, isPublic: boolean): boolean {
    if (isPublic) {
        return PUBLIC_PATHS.some(p => path === p || path.startsWith(p + '/'));
    }
    return ALLOWED_PATH_PREFIXES.some(prefix =>
        path === prefix.replace(/\/$/, '') || path.startsWith(prefix)
    );
}

async function proxyFetch(request: NextRequest, path: string, method: string) {
    const isPublic = PUBLIC_PATHS.some(p => path === p || path.startsWith(p + '/'));

    if (!isPathAllowed(path, isPublic)) {
        return NextResponse.json({ error: 'Ruta no permitida por el proxy' }, { status: 403 });
    }

    const cookieStore = await cookies();
    const token = cookieStore.get('erp_token')?.value;
    const tenantId = cookieStore.get('tenantId')?.value;

    if (!isPublic) {
        if (!token) {
            return NextResponse.json({ error: 'No autenticado' }, { status: 401 });
        }
        if (!tenantId) {
            return NextResponse.json({ error: 'Tenant ID no configurado' }, { status: 400 });
        }
    }

    // API_URL se usa server-side (no NEXT_PUBLIC_) para que funcione dentro de Docker
    const backendUrl = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
    const url = `${backendUrl}/api/${path}`;

    // multipart/form-data (subida de archivos, p. ej. documents) no puede
    // reenviarse como si fuera JSON: forzar Content-Type: application/json
    // aquí rompía el body y el boundary del multipart. Se reenvía tal cual,
    // con su propio Content-Type (incluye el boundary real).
    const requestContentType = request.headers.get('content-type') ?? '';
    const isMultipart = requestContentType.startsWith('multipart/form-data');

    const headers: Record<string, string> = {
        'Content-Type': isMultipart ? requestContentType : 'application/json',
        ...(token && { 'Authorization': `Bearer ${token}` }),
        ...(tenantId && { 'X-Tenant-Id': tenantId }),
    };

    try {
        let body: string | ArrayBuffer | undefined;
        if (['POST', 'PUT', 'PATCH'].includes(method)) {
            body = isMultipart ? await request.arrayBuffer() : await request.text();
        }

        const response = await fetch(url, { method, headers, body, redirect: 'manual' });

        if (response.status === 401) {
            cookieStore.delete('erp_token');
            cookieStore.delete('tenantId');
            cookieStore.delete('tenantName');
            return NextResponse.json({ error: 'Token expirado' }, { status: 401 });
        }

        // For binary responses (CSV, XML, PDF…) stream the raw body with original headers
        const contentType = response.headers.get('content-type') ?? '';
        if (!contentType.includes('application/json')) {
            const blob = await response.blob();
            const responseHeaders = new Headers();
            responseHeaders.set('content-type', contentType);
            const disposition = response.headers.get('content-disposition');
            if (disposition) responseHeaders.set('content-disposition', disposition);
            return new NextResponse(blob, { status: response.status, headers: responseHeaders });
        }

        const responseData = await response.json();
        return NextResponse.json(responseData, { status: response.status });
    } catch (error) {
        return NextResponse.json({
            error: 'Error de conexión con el backend',
            details: error instanceof Error ? error.message : 'Error desconocido',
        }, { status: 502 });
    }
}

export async function GET(request: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
    const { path } = await params;
    return proxyFetch(request, path.join('/'), 'GET');
}

export async function POST(request: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
    const { path } = await params;
    return proxyFetch(request, path.join('/'), 'POST');
}

export async function PUT(request: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
    const { path } = await params;
    return proxyFetch(request, path.join('/'), 'PUT');
}

export async function PATCH(request: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
    const { path } = await params;
    return proxyFetch(request, path.join('/'), 'PATCH');
}

export async function DELETE(request: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
    const { path } = await params;
    return proxyFetch(request, path.join('/'), 'DELETE');
}
