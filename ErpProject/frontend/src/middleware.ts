import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

export function middleware(request: NextRequest) {
    const token = request.cookies.get('erp_token');
    const tenantId = request.cookies.get('tenantId');
    const { pathname } = request.nextUrl;

    // Para API routes, pasar headers de autenticación
    if (pathname.startsWith('/api/proxy')) {
        const requestHeaders = new Headers(request.headers);

        if (token?.value) {
            requestHeaders.set('Authorization', `Bearer ${token.value}`);
        }

        if (tenantId?.value) {
            requestHeaders.set('X-Tenant-Id', tenantId.value);
        }

        requestHeaders.set('Content-Type', 'application/json');

        return NextResponse.next({
            request: {
                headers: requestHeaders,
            },
        });
    }

    // Proteger rutas privadas
    const protectedRoutes = [
        '/dashboard',
        '/crm',
        '/billing',
        '/accounting',
        '/inventory',
        '/expenses',
        '/settings',
    ];

    const isProtected = protectedRoutes.some(route => pathname.startsWith(route));

    if (isProtected && !token) {
        return NextResponse.redirect(new URL('/admin', request.url));
    }

    // Redirigir usuarios autenticados lejos de login/registro
    if ((pathname === '/admin' || pathname === '/register' || pathname === '/signup' || pathname === '/') && token) {
        return NextResponse.redirect(new URL('/dashboard', request.url));
    }

    return NextResponse.next();
}

export const config = {
    matcher: [
        '/',
        '/dashboard/:path*',
        '/crm/:path*',
        '/billing/:path*',
        '/accounting/:path*',
        '/inventory/:path*',
        '/expenses/:path*',
        '/settings/:path*',
        '/admin',
        '/register',
        '/signup',
        '/api/proxy/:path*',
    ],
};
