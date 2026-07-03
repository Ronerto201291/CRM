'use client';

import { useActionState } from 'react';
import { loginAction, verifyTotpAction } from '@/app/actions/auth';
import Logo from '@/components/Logo';
import FormLabel from '@/components/FormLabel';

const inputStyle = {
    width: '100%', padding: '10px 14px',
    background: 'rgba(255,255,255,0.06)',
    border: '1px solid rgba(255,255,255,0.12)',
    borderRadius: '8px', fontSize: '14px',
    color: 'white', outline: 'none',
    fontFamily: 'Inter, sans-serif',
    transition: 'border 0.15s',
} as const;

const btnStyle = (pending: boolean) => ({
    marginTop: '8px', padding: '12px', width: '100%',
    background: pending ? 'rgba(37,99,235,0.5)' : 'linear-gradient(135deg, #2563eb, #1d4ed8)',
    border: 'none', borderRadius: '8px',
    fontSize: '14px', fontWeight: 700, color: 'white',
    cursor: pending ? 'not-allowed' : 'pointer',
    boxShadow: '0 4px 12px rgba(37,99,235,0.35)',
    transition: 'all 0.15s', fontFamily: 'Inter, sans-serif',
} as const);

export default function AdminPage() {
    const [loginState, loginFormAction, loginPending] = useActionState(loginAction, null);
    const [totpState, totpFormAction, totpPending] = useActionState(verifyTotpAction, null);

    // If backend says 2FA required, switch to TOTP step
    const needs2FA = (loginState as { requiresTwoFactor?: boolean; userId?: string } | null)?.requiresTwoFactor;
    const pendingUserId = (loginState as { userId?: string } | null)?.userId;

    const bg = (
        <div style={{ position: 'fixed', inset: 0, overflow: 'hidden', pointerEvents: 'none' }}>
            <div style={{ position: 'absolute', top: '-20%', right: '-10%', width: '600px', height: '600px', borderRadius: '50%', background: 'radial-gradient(circle, rgba(37,99,235,0.12) 0%, transparent 70%)' }} />
            <div style={{ position: 'absolute', bottom: '-20%', left: '-10%', width: '500px', height: '500px', borderRadius: '50%', background: 'radial-gradient(circle, rgba(14,165,233,0.10) 0%, transparent 70%)' }} />
        </div>
    );

    const logo = (
        <div style={{ textAlign: 'center', marginBottom: '36px' }}>
            <div style={{ display: 'flex', justifyContent: 'center', marginBottom: '16px' }}>
                <Logo size={140} />
            </div>
            <p style={{ fontSize: '13px', color: 'rgba(255,255,255,0.45)' }}>Gestión empresarial para PYMES españolas</p>
        </div>
    );

    const card = (children: React.ReactNode) => (
        <div style={{ background: 'rgba(255,255,255,0.04)', border: '1px solid rgba(255,255,255,0.08)', borderRadius: '16px', padding: '32px', backdropFilter: 'blur(16px)' }}>
            {children}
        </div>
    );

    return (
        <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #0f172a 100%)', padding: '20px', fontFamily: 'Inter, sans-serif' }}>
            {bg}
            <div style={{ width: '100%', maxWidth: '420px', position: 'relative', zIndex: 1 }}>
                {logo}

                {/* STEP 1 — Email + Password */}
                {!needs2FA && card(
                    <>
                        <h2 style={{ fontSize: '18px', fontWeight: 700, color: 'white', marginBottom: '24px' }}>Iniciar sesión</h2>

                        {(loginState as { error?: string } | null)?.error && (
                            <div style={{ marginBottom: '16px', padding: '10px 14px', background: 'rgba(239,68,68,0.12)', border: '1px solid rgba(239,68,68,0.3)', borderRadius: '8px', fontSize: '13px', color: '#fca5a5' }}>
                                {(loginState as { error: string }).error}
                            </div>
                        )}

                        <form style={{ display: 'flex', flexDirection: 'column', gap: '16px' }} action={loginFormAction}>
                            <div>
                                <FormLabel htmlFor="login-email" required>Email</FormLabel>
                                <input id="login-email" name="email" type="email" required placeholder="admin@empresa.com" style={inputStyle}
                                    onFocus={e => (e.target.style.borderColor = 'rgba(37,99,235,0.6)')}
                                    onBlur={e => (e.target.style.borderColor = 'rgba(255,255,255,0.12)')} />
                            </div>
                            <div>
                                <FormLabel htmlFor="login-password" required>Contraseña</FormLabel>
                                <input id="login-password" name="password" type="password" required placeholder="••••••••" style={inputStyle}
                                    onFocus={e => (e.target.style.borderColor = 'rgba(37,99,235,0.6)')}
                                    onBlur={e => (e.target.style.borderColor = 'rgba(255,255,255,0.12)')} />
                            </div>
                            <button type="submit" disabled={loginPending} style={btnStyle(loginPending)}>
                                {loginPending ? 'Iniciando sesión...' : 'Iniciar sesión →'}
                            </button>
                        </form>
                    </>
                )}

                {/* STEP 2 — TOTP / 2FA */}
                {needs2FA && card(
                    <>
                        <div style={{ textAlign: 'center', marginBottom: '20px' }}>
                            <div style={{ fontSize: '32px', marginBottom: '8px' }}>🔐</div>
                            <h2 style={{ fontSize: '18px', fontWeight: 700, color: 'white', marginBottom: '6px' }}>Verificación en dos pasos</h2>
                            <p style={{ fontSize: '13px', color: 'rgba(255,255,255,0.45)' }}>Introduce el código de tu aplicación autenticadora</p>
                        </div>

                        {(totpState as { error?: string } | null)?.error && (
                            <div style={{ marginBottom: '16px', padding: '10px 14px', background: 'rgba(239,68,68,0.12)', border: '1px solid rgba(239,68,68,0.3)', borderRadius: '8px', fontSize: '13px', color: '#fca5a5' }}>
                                {(totpState as { error: string }).error}
                            </div>
                        )}

                        <form style={{ display: 'flex', flexDirection: 'column', gap: '16px' }} action={totpFormAction}>
                            <input type="hidden" name="userId" value={pendingUserId ?? ''} />
                            <div>
                                <FormLabel htmlFor="totp-code" required>Código 2FA</FormLabel>
                                <input id="totp-code" name="code" type="text" inputMode="numeric" pattern="[0-9]*" maxLength={6} required
                                    placeholder="000000"
                                    style={{ ...inputStyle, fontSize: '24px', textAlign: 'center', letterSpacing: '0.2em' }}
                                    onFocus={e => (e.target.style.borderColor = 'rgba(37,99,235,0.6)')}
                                    onBlur={e => (e.target.style.borderColor = 'rgba(255,255,255,0.12)')} />
                            </div>
                            <button type="submit" disabled={totpPending} style={btnStyle(totpPending)}>
                                {totpPending ? 'Verificando...' : 'Verificar →'}
                            </button>
                        </form>
                    </>
                )}

                <p style={{ textAlign: 'center', marginTop: '20px', fontSize: '12px', color: 'rgba(255,255,255,0.2)' }}>
                    © 2026 ERP SaaS · Cumplimiento normativa fiscal española
                </p>
            </div>
        </div>
    );
}
