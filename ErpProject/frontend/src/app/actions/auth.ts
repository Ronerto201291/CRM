'use server';

import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';

const cookieOpts = (httpOnly = true) => ({
    httpOnly,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax' as const,
    path: '/',
    maxAge: 60 * 60 * 8, // 8 hours
});

export async function loginAction(prevState: unknown, formData: FormData) {
    const email = formData.get('email');
    const password = formData.get('password');

    if (!email || !password) {
        return { error: 'Email y contraseña son obligatorios' };
    }

    try {
        const backendUrl = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
        const response = await fetch(`${backendUrl}/api/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password }),
        });

        const data = await response.json();

        if (!response.ok) {
            return { error: data.error || 'Credenciales inválidas' };
        }

        // 2FA required — do NOT issue JWT yet; return partial state for second step
        if (data.requiresTwoFactor) {
            const cookieStore = await cookies();
            // Store userId temporarily so the 2FA verify step can use it
            cookieStore.set('twofa_pending_userId', String(data.userId), { ...cookieOpts(true), maxAge: 60 * 5 }); // 5 min
            return { requiresTwoFactor: true, userId: String(data.userId) };
        }

        await setAuthCookies(data);
    } catch (error) {
        console.error('Login error:', error);
        return { error: 'Error de conexión con el servidor. Verifica que el backend está corriendo.' };
    }

    redirect('/dashboard');
}

export async function verifyTotpAction(prevState: unknown, formData: FormData) {
    const code = formData.get('code');
    const userId = formData.get('userId');

    if (!code || !userId) {
        return { error: 'Código requerido' };
    }

    try {
        const backendUrl = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
        const response = await fetch(`${backendUrl}/api/auth/2fa/verify`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ userId, code }),
        });

        const data = await response.json();

        if (!response.ok || !data.token) {
            return { error: data.error || 'Código incorrecto' };
        }

        const cookieStore = await cookies();
        cookieStore.delete('twofa_pending_userId');
        await setAuthCookies(data);
    } catch (error) {
        console.error('2FA verify error:', error);
        return { error: 'Error de conexión con el servidor' };
    }

    redirect('/dashboard');
}

async function setAuthCookies(data: {
    token: string;
    userId?: string;
    email?: string;
    companyId?: string;
    companyName?: string;
}) {
    const cookieStore = await cookies();

    cookieStore.set('erp_token', data.token, cookieOpts(true));

    cookieStore.set('tenantId', data.companyId ?? '', cookieOpts(false));

    cookieStore.set('tenantName', data.companyName || (data.email?.split('@')[0] ?? ''), cookieOpts(false));
    cookieStore.set('userId', String(data.userId ?? ''), cookieOpts(true));
}

export async function registerCompanyAction(prevState: unknown, formData: FormData) {
    const companyName    = formData.get('companyName');
    const companyTaxId   = formData.get('companyTaxId');
    const companyAddress = formData.get('companyAddress');
    const adminEmail     = formData.get('adminEmail');
    const adminFirstName = formData.get('adminFirstName');
    const adminLastName  = formData.get('adminLastName');
    const adminPassword  = formData.get('adminPassword');
    const confirmPassword = formData.get('confirmPassword');

    if (!companyName || !companyTaxId || !adminEmail || !adminFirstName || !adminPassword) {
        return { error: 'Rellena todos los campos obligatorios' };
    }
    if (adminPassword !== confirmPassword) {
        return { error: 'Las contraseñas no coinciden' };
    }
    if (String(adminPassword).length < 8) {
        return { error: 'La contraseña debe tener al menos 8 caracteres' };
    }

    try {
        const backendUrl = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
        const response = await fetch(`${backendUrl}/api/auth/register`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                companyName, companyTaxId, companyAddress,
                adminEmail, adminFirstName, adminLastName, adminPassword,
            }),
        });

        const data = await response.json();

        if (!response.ok) {
            return { error: data.error || data.message || 'Error al registrar la empresa' };
        }

        await setAuthCookies(data);
    } catch (error) {
        console.error('Register error:', error);
        return { error: 'Error de conexión con el servidor' };
    }

    redirect('/dashboard');
}

export async function acceptInviteAction(prevState: unknown, formData: FormData) {
    const token          = formData.get('token');
    const taxId          = formData.get('taxId');
    const firstName      = formData.get('firstName');
    const lastName       = formData.get('lastName');
    const password       = formData.get('password');
    const confirmPassword = formData.get('confirmPassword');

    if (!token || !taxId || !firstName || !password) {
        return { error: 'Todos los campos obligatorios son requeridos' };
    }
    if (password !== confirmPassword) {
        return { error: 'Las contraseñas no coinciden' };
    }

    try {
        const backendUrl = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
        const response = await fetch(`${backendUrl}/api/auth/accept-invite`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                token, taxId, firstName, lastName, password
            }),
        });

        const data = await response.json();

        if (!response.ok) {
            return { error: data.error || data.message || 'Error al aceptar la invitación y configurar la cuenta' };
        }

        await setAuthCookies(data);
    } catch (error) {
        console.error('Accept Invite error:', error);
        return { error: 'Error de conexión con el servidor' };
    }

    redirect('/dashboard');
}

export async function logoutAction() {
    const cookieStore = await cookies();
    cookieStore.delete('erp_token');
    cookieStore.delete('tenantId');
    cookieStore.delete('tenantName');
    cookieStore.delete('userId');
    cookieStore.delete('twofa_pending_userId');
    redirect('/admin');
}
