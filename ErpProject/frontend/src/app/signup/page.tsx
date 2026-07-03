'use client';
import { useActionState } from 'react';
import { registerCompanyAction } from '../actions/auth';
import Link from 'next/link';
import FormLabel from '@/components/FormLabel';

const PLANS = [
    { name: 'Free',         price: '0€/mes',   features: ['Facturación + CRM', '20 facturas/mes', '1 usuario'] },
    { name: 'Starter',      price: '19€/mes',  features: ['+ Gastos + Contabilidad', '100 facturas/mes', '3 usuarios'] },
    { name: 'Professional', price: '49€/mes',  features: ['+ Inventario + OCR', '500 facturas/mes', '10 usuarios'] },
    { name: 'Enterprise',   price: '99€/mes',  features: ['+ API Pública', 'Ilimitado', 'Usuarios ilimitados'] },
];

export default function SignupPage() {
    const [state, action, pending] = useActionState(registerCompanyAction, null);

    return (
        <div style={{
            minHeight: '100vh', background: 'var(--bg)', fontFamily: 'Inter, sans-serif',
            display: 'flex', flexDirection: 'column', alignItems: 'center',
            padding: '40px 20px',
        }}>
            {/* Header */}
            <div style={{ textAlign: 'center', marginBottom: '36px' }}>
                <img src="/logo.png" alt="Orbital ERP" style={{ width: '48px', height: '48px', objectFit: 'contain', marginBottom: '12px' }} />
                <h1 style={{ fontSize: '26px', fontWeight: 800, color: 'var(--text-primary)', margin: 0 }}>
                    Crea tu empresa en Orbital ERP
                </h1>
                <p style={{ fontSize: '14px', color: 'var(--text-muted)', marginTop: '8px' }}>
                    Empieza gratis. Actualiza cuando lo necesites.
                </p>
            </div>

            <div style={{ width: '100%', maxWidth: '960px', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '28px', alignItems: 'start' }}>

                {/* ─── Registration Form ─── */}
                <div className="erp-card" style={{ padding: '32px' }}>
                    <h2 style={{ fontSize: '17px', fontWeight: 700, marginBottom: '24px', color: 'var(--text-primary)' }}>
                        Datos de tu empresa
                    </h2>

                    {state?.error && (
                        <div style={{
                            background: 'var(--danger-bg)', border: '1px solid rgba(239,68,68,0.2)',
                            borderRadius: '8px', padding: '10px 14px', marginBottom: '20px',
                            fontSize: '13px', color: 'var(--danger)',
                        }}>
                            ⚠️ {state.error}
                        </div>
                    )}

                    <form action={action}>
                        <section style={{ marginBottom: '24px' }}>
                            <div style={{ fontSize: '10px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.08em', marginBottom: '12px', borderBottom: '1px solid var(--border)', paddingBottom: '6px' }}>
                                Empresa
                            </div>
                            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                                <div>
                                    <FormLabel htmlFor="signup-companyName" required>Nombre de la empresa</FormLabel>
                                    <input id="signup-companyName" className="erp-input" name="companyName" placeholder="Empresa S.L." required />
                                </div>
                                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                                    <div>
                                        <FormLabel htmlFor="signup-companyTaxId" required>CIF / NIF</FormLabel>
                                        <input id="signup-companyTaxId" className="erp-input" name="companyTaxId" placeholder="B12345678" required />
                                    </div>
                                    <div>
                                        <FormLabel htmlFor="signup-companyAddress">Dirección</FormLabel>
                                        <input id="signup-companyAddress" className="erp-input" name="companyAddress" placeholder="Calle, ciudad" />
                                    </div>
                                </div>
                            </div>
                        </section>

                        <section style={{ marginBottom: '24px' }}>
                            <div style={{ fontSize: '10px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.08em', marginBottom: '12px', borderBottom: '1px solid var(--border)', paddingBottom: '6px' }}>
                                Administrador
                            </div>
                            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                                <div>
                                    <FormLabel htmlFor="signup-adminEmail" required>Email</FormLabel>
                                    <input id="signup-adminEmail" className="erp-input" name="adminEmail" type="email" placeholder="admin@empresa.com" required />
                                </div>
                                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                                    <div>
                                        <FormLabel htmlFor="signup-adminFirstName" required>Nombre</FormLabel>
                                        <input id="signup-adminFirstName" className="erp-input" name="adminFirstName" placeholder="María" required />
                                    </div>
                                    <div>
                                        <FormLabel htmlFor="signup-adminLastName">Apellidos</FormLabel>
                                        <input id="signup-adminLastName" className="erp-input" name="adminLastName" placeholder="García López" />
                                    </div>
                                </div>
                                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                                    <div>
                                        <FormLabel htmlFor="signup-adminPassword" required>Contraseña (mín. 8 caracteres)</FormLabel>
                                        <input id="signup-adminPassword" className="erp-input" name="adminPassword" type="password" placeholder="••••••••" required minLength={8} />
                                    </div>
                                    <div>
                                        <FormLabel htmlFor="signup-confirmPassword" required>Confirmar contraseña</FormLabel>
                                        <input id="signup-confirmPassword" className="erp-input" name="confirmPassword" type="password" placeholder="••••••••" required />
                                    </div>
                                </div>
                            </div>
                        </section>

                        <button
                            type="submit"
                            className="btn btn-primary"
                            disabled={pending}
                            style={{ width: '100%', padding: '13px', fontSize: '14px', fontWeight: 700 }}
                        >
                            {pending ? 'Creando cuenta...' : '🚀 Crear cuenta gratis'}
                        </button>

                        <p style={{ textAlign: 'center', fontSize: '11px', color: 'var(--text-muted)', marginTop: '14px' }}>
                            Al registrarte aceptas los términos de uso. Empiezas con el plan <strong>Free</strong> (0€/mes).
                        </p>
                    </form>

                    <div style={{ borderTop: '1px solid var(--border)', marginTop: '20px', paddingTop: '16px', textAlign: 'center', fontSize: '13px', color: 'var(--text-muted)' }}>
                        ¿Ya tienes cuenta?{' '}
                        <Link href="/" style={{ color: 'var(--brand-primary)', fontWeight: 600, textDecoration: 'none' }}>
                            Iniciar sesión
                        </Link>
                    </div>
                </div>

                {/* ─── Plans ─── */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                    <h3 style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-secondary)', margin: '0 0 4px', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                        Planes disponibles
                    </h3>
                    {PLANS.map((p, i) => (
                        <div key={p.name} className="erp-card" style={{
                            padding: '16px 20px',
                            borderColor: i === 0 ? 'var(--brand-primary)' : 'var(--border)',
                            borderWidth: i === 0 ? '2px' : '1px',
                            position: 'relative',
                        }}>
                            {i === 0 && (
                                <span style={{
                                    position: 'absolute', top: '-10px', left: '16px',
                                    background: 'var(--brand-primary)', color: 'white',
                                    fontSize: '10px', fontWeight: 700, padding: '2px 10px', borderRadius: '20px',
                                }}>EMPIEZA AQUÍ</span>
                            )}
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                                <span style={{ fontWeight: 700, fontSize: '15px', color: 'var(--text-primary)' }}>{p.name}</span>
                                <span style={{ fontWeight: 800, fontSize: '14px', color: i === 0 ? 'var(--brand-primary)' : 'var(--text-secondary)' }}>{p.price}</span>
                            </div>
                            <ul style={{ margin: 0, padding: '0 0 0 14px', fontSize: '12px', color: 'var(--text-muted)', lineHeight: '1.7' }}>
                                {p.features.map(f => <li key={f}>{f}</li>)}
                            </ul>
                        </div>
                    ))}
                    <p style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '4px' }}>
                        Puedes cambiar de plan en cualquier momento desde Configuración → Suscripción.
                    </p>
                </div>
            </div>
        </div>
    );
}
