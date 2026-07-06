'use server';

import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';
import {
    loginSchema,
    totpSchema,
    signupSchema,
    registerInviteSchema,
} from '@/lib/schemas/authSchemas';

const cookieOpts = (httpOnly = true) => ({
    httpOnly,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax' as const,
    path: '/',
    maxAge: 60 * 60 * 8, // 8 hours
});

export async function loginAction(prevState: unknown, formData: FormData) {
    const parsed = loginSchema.safeParse({
        email: formData.get('email'),
        password: formData.get('password'),
    });

    if (!parsed.success) {
        return { error: parsed.error.errors[0]?.message ?? 'Datos inválidos' };
    }

    const { email, password } = parsed.data;

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
    const parsed = totpSchema.safeParse({
        code: formData.get('code'),
        userId: formData.get('userId'),
    });

    if (!parsed.success) {
        return { error: parsed.error.errors[0]?.message ?? 'Código inválido' };
    }

    const { code, userId } = parsed.data;

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
    const parsed = signupSchema.safeParse({
        companyName: formData.get('companyName'),
        companyTaxId: formData.get('companyTaxId'),
        companyAddress: formData.get('companyAddress') || undefined,
        adminEmail: formData.get('adminEmail'),
        adminFirstName: formData.get('adminFirstName'),
        adminLastName: formData.get('adminLastName') || undefined,
        adminPassword: formData.get('adminPassword'),
        confirmPassword: formData.get('confirmPassword'),
    });

    if (!parsed.success) {
        return { error: parsed.error.errors[0]?.message ?? 'Datos inválidos' };
    }

    const {
        companyName, companyTaxId, companyAddress,
        adminEmail, adminFirstName, adminLastName, adminPassword,
    } = parsed.data;

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
    const parsed = registerInviteSchema.safeParse({
        token: formData.get('token'),
        taxId: formData.get('taxId'),
        firstName: formData.get('firstName'),
        lastName: formData.get('lastName') || undefined,
        password: formData.get('password'),
        confirmPassword: formData.get('confirmPassword'),
    });

    if (!parsed.success) {
        return { error: parsed.error.errors[0]?.message ?? 'Datos inválidos' };
    }

    const { token, taxId, firstName, lastName, password } = parsed.data;

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
