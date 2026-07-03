'use client';
import { useActionState, Suspense } from 'react';
import { acceptInviteAction } from '../actions/auth';
import { useSearchParams } from 'next/navigation';
import Link from 'next/link';
import FormLabel from '@/components/FormLabel';

const INIT = { error: undefined as string | undefined };

function RegisterForm() {
    const [state, formAction, pending] = useActionState(acceptInviteAction, INIT);
    const searchParams = useSearchParams();
    const token = searchParams.get('token');

    if (!token) {
        return (
            <div style={{ textAlign: 'center', padding: '40px 20px' }}>
                <h2 style={{ fontSize: '20px', color: 'var(--text-primary)', marginBottom: '16px' }}>Acceso Privado</h2>
                <p style={{ color: 'var(--text-muted)', fontSize: '14px', lineHeight: 1.6 }}>
                    Orbital ERP es una plataforma SaaS por invitación.<br />
                    Contacte a su administrador para obtener un enlace de invitación.
                </p>
                <div style={{ marginTop: '24px' }}>
                    <Link href="/" className="btn btn-primary" style={{ padding: '10px 20px', textDecoration: 'none', display: 'inline-block' }}>
                        Volver al inicio de sesión
                    </Link>
                </div>
            </div>
        );
    }

    return (
        <div className="erp-card" style={{ padding: '32px' }}>
            <form action={formAction}>
                <input type="hidden" name="token" value={token} />
                
                <p style={{ fontSize: '11px', fontWeight: 700, color: 'var(--brand-primary)', letterSpacing: '0.08em', marginBottom: '14px' }}>
                    1. DATOS FISCALES DE EMPRESA
                </p>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '12px', marginBottom: '12px' }}>
                    <div className="form-group">
                        <FormLabel htmlFor="register-taxId" required>CIF / NIF definitivo</FormLabel>
                        <input id="register-taxId" className="erp-input" name="taxId" placeholder="B12345678" required />
                        <p style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '4px' }}>Obligatorio para emitir facturas legales (Verifactu).</p>
                    </div>
                </div>

                <div style={{ borderTop: '1px solid var(--border)', margin: '20px 0 16px' }} />

                <p style={{ fontSize: '11px', fontWeight: 700, color: 'var(--brand-primary)', letterSpacing: '0.08em', marginBottom: '14px' }}>
                    2. CONFIGURAR ADMINISTRADOR
                </p>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
                    <div className="form-group">
                        <FormLabel htmlFor="register-firstName" required>Nombre</FormLabel>
                        <input id="register-firstName" className="erp-input" name="firstName" placeholder="Nombre" required />
                    </div>
                    <div className="form-group">
                        <FormLabel htmlFor="register-lastName">Apellidos</FormLabel>
                        <input id="register-lastName" className="erp-input" name="lastName" placeholder="Apellidos" />
                    </div>
                    <div className="form-group">
                        <FormLabel htmlFor="register-password" required>Contraseña</FormLabel>
                        <input id="register-password" className="erp-input" type="password" name="password" placeholder="Mínimo 8 caracteres" required minLength={8} />
                    </div>
                    <div className="form-group">
                        <FormLabel htmlFor="register-confirmPassword" required>Confirmar contraseña</FormLabel>
                        <input id="register-confirmPassword" className="erp-input" type="password" name="confirmPassword" placeholder="Repite la contraseña" required minLength={8} />
                    </div>
                </div>

                {state?.error && (
                    <div style={{
                        background: 'var(--danger-bg)', border: '1px solid rgba(239,68,68,0.2)',
                        borderRadius: '8px', padding: '10px 14px', marginBottom: '16px',
                        fontSize: '13px', color: 'var(--danger)', display: 'flex', gap: '8px', alignItems: 'center',
                    }}>
                        ⚠️ {state.error}
                    </div>
                )}

                <button
                    type="submit"
                    className="btn btn-primary"
                    disabled={pending}
                    style={{ width: '100%', padding: '12px', fontSize: '14px', fontWeight: 700, marginTop: '8px' }}
                >
                    {pending ? 'Completando configuración...' : 'Completar registro →'}
                </button>
            </form>
        </div>
    );
}

export default function RegisterPage() {
    return (
        <div style={{
            minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center',
            background: 'var(--bg)', fontFamily: 'Inter, sans-serif', padding: '24px 20px',
        }}>
            <div style={{ width: '100%', maxWidth: '520px' }}>
                <div style={{ textAlign: 'center', marginBottom: '32px' }}>
                    <div style={{ marginBottom: '16px' }}>
                        <img src="/logo.png" alt="Orbital ERP" style={{ width: '52px', height: '52px', objectFit: 'contain' }} />
                    </div>
                    <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-primary)', marginBottom: '6px' }}>
                        Bienvenido a Orbital ERP
                    </h1>
                    <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
                        Complete su registro para acceder al entorno empresarial
                    </p>
                </div>

                <Suspense fallback={<div style={{ textAlign: 'center', color: 'var(--text-muted)' }}>Cargando validación segura...</div>}>
                    <RegisterForm />
                </Suspense>

                <p style={{ textAlign: 'center', fontSize: '13px', color: 'var(--text-muted)', marginTop: '20px' }}>
                    <Link href="/" style={{ color: 'var(--brand-primary)', fontWeight: 600, textDecoration: 'none' }}>
                        Volver a inicio de sesión
                    </Link>
                </p>
            </div>
        </div>
    );
}
