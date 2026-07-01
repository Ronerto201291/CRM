import { cookies } from 'next/headers';
import { NextRequest, NextResponse } from 'next/server';

// Rutas que no requieren token ni tenantId (login, registro, etc.)
const PUBLIC_PATHS = ['auth/login', 'auth/register', 'auth/refresh', 'auth/2fa', 'v1/public'];

const PROXY_PATHS = [
    'invoices', 'clients', 'quotes', 'leads',
    'inventory/products', 'inventory/warehouses', 'inventory/stock', 'inventory/movements',
    'purchasing/orders', 'purchasing/invoices', 'purchasing/receipts',
];

async function proxyFetch(request: NextRequest, path: string, method: string) {
    const isPublic = PUBLIC_PATHS.some(p => path.startsWith(p));

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
    const headers: Record<string, string> = {
        'Content-Type': 'application/json',
        ...(token && { 'Authorization': `Bearer ${token}` }),
        ...(tenantId && { 'X-Tenant-Id': tenantId }),
    };

    try {
        let body: string | undefined;
        if (['POST', 'PUT', 'PATCH'].includes(method)) {
            body = await request.text();
        }

        const response = await fetch(url, { method, headers, body, redirect: 'manual' });

        if (response.status === 401) {
            cookieStore.delete('erp_token');
            cookieStore.delete('tenantId');
            cookieStore.delete('tenantName');
            return NextResponse.json({ error: 'Token expirado' }, { status: 401 });
        }

        // For binary responses (CSV, XML, PDFâ€¦) stream the raw body with original headers
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
        // If response body couldn't be parsed as JSON, return raw error info
        const isContentTypeJson = response.headers.get('content-type')?.includes('application/json') ?? false;
        if (isContentTypeJson) {
            return NextResponse.json({
                error: 'Error de conexión con el backend',
                details: error instanceof Error ? error.message : 'Error desconocido'
            }, { status: 500 });
        }
        // Non-JSON error response (e.g. HTML error page from a proxy)
        return NextResponse.json({
            error: 'Error del servidor backend',
            status: response.status
        }, { status: response.status >= 400 ? response.status : 500 });
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
