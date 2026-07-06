'use client';
import { useState } from 'react';
import AccessibleModal from '@/components/AccessibleModal';
import { daysUntil } from '@/lib/time';

interface Company {
    id: string;
    name: string;
    taxId: string;
    isActive: boolean;
    country: string;
    createdAt: string;
    planName: string;
    subscriptionActive: boolean;
}

interface Invitation {
    id: string;
    email: string;
    token: string;
    isUsed: boolean;
    expiresAt: string;
    companyId: string;
    companyName: string;
}

interface InviteResult {
    companyId: string;
    invitationId: string;
    token: string;
    email: string;
    inviteUrlPath: string;
}

const PLAN_COLORS: Record<string, string> = {
    Free: '#6b7280',
    Starter: '#3b82f6',
    Professional: '#8b5cf6',
    Enterprise: '#f59e0b',
    Pro: '#10b981',
};

export default function EmpresasClient({
    initialCompanies,
    initialInvitations,
    initialForbidden,
}: {
    initialCompanies: Company[];
    initialInvitations: Invitation[];
    initialForbidden: boolean;
}) {
    const [companies, setCompanies] = useState<Company[]>(initialCompanies);
    const [invitations, setInvitations] = useState<Invitation[]>(initialInvitations);
    const [loading, setLoading] = useState(false);
    const [forbidden, setForbidden] = useState(initialForbidden);

    const [showModal, setShowModal] = useState(false);
    const [form, setForm] = useState({ companyName: '', adminEmail: '' });
    const [saving, setSaving] = useState(false);
    const [inviteResult, setInviteResult] = useState<InviteResult | null>(null);
    const [error, setError] = useState('');
    const [copied, setCopied] = useState(false);
    const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

    const load = async () => {
        setLoading(true);
        const [cRes, iRes] = await Promise.all([
            fetch('/api/proxy/admin/companies'),
            fetch('/api/proxy/admin/invitations'),
        ]);

        if (cRes.status === 403 || iRes.status === 403) {
            setForbidden(true);
            setLoading(false);
            return;
        }

        if (cRes.ok) setCompanies(await cRes.json());
        if (iRes.ok) setInvitations(await iRes.json());
        setLoading(false);
    };

    const showMsg = (type: 'success' | 'error', text: string) => {
        setMessage({ type, text });
        setTimeout(() => setMessage(null), 5000);
    };

    const handleInvite = async () => {
        if (!form.companyName || !form.adminEmail) {
            setError('Nombre de empresa y email son obligatorios');
            return;
        }
        setSaving(true);
        setError('');
        const res = await fetch('/api/proxy/admin/invite', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ companyName: form.companyName, adminEmail: form.adminEmail }),
        });
        setSaving(false);
        if (res.ok) {
            const data: InviteResult = await res.json();
            setInviteResult(data);
            setForm({ companyName: '', adminEmail: '' });
            load();
        } else {
            const err = await res.json().catch(() => ({}));
            setError(err.error || err.message || 'Error al generar la invitación');
        }
    };

    const copyInviteLink = (token: string) => {
        const url = `${window.location.origin}/register?token=${token}`;
        navigator.clipboard.writeText(url);
        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
        showMsg('success', 'Enlace copiado al portapapeles');
    };

    const pendingInvitations = invitations.filter(i => !i.isUsed && new Date(i.expiresAt) > new Date());
    const usedInvitations = invitations.filter(i => i.isUsed || new Date(i.expiresAt) <= new Date());

    if (loading) return (
        <div style={{ padding: '40px 32px', fontFamily: 'Inter, sans-serif', color: 'var(--text-muted)', fontSize: '14px' }}>
            Cargando empresas...
        </div>
    );

    if (forbidden) return (
        <div style={{ padding: '40px 32px', fontFamily: 'Inter, sans-serif' }}>
            <div style={{
                background: 'var(--danger-bg)', border: '1px solid rgba(239,68,68,0.2)',
                borderRadius: '10px', padding: '24px', maxWidth: '500px',
            }}>
                <div style={{ fontSize: '28px', marginBottom: '12px' }}>🔒</div>
                <h2 style={{ fontSize: '16px', fontWeight: 700, color: 'var(--danger)', marginBottom: '8px' }}>
                    Acceso restringido
                </h2>
                <p style={{ fontSize: '13px', color: 'var(--text-secondary)', lineHeight: 1.6 }}>
                    Esta sección es exclusiva del administrador raíz de la plataforma Orbital ERP.
                    Contacta al soporte técnico si necesitas acceso.
                </p>
            </div>
        </div>
    );

    return (
        <div style={{ padding: '28px 32px', fontFamily: 'Inter, sans-serif' }}>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Gestión de Empresas</h1>
                    <p className="page-subtitle">Tenants · Invitaciones · Planes</p>
                </div>
                <button className="btn btn-primary" onClick={() => { setInviteResult(null); setError(''); setShowModal(true); }}>
                    <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                        <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
                    </svg>
                    Invitar empresa
                </button>
            </div>

            {message && (
                <div style={{
                    marginBottom: '16px', padding: '10px 14px', borderRadius: '8px', fontSize: '13px',
                    background: message.type === 'success' ? 'var(--success-bg)' : 'var(--danger-bg)',
                    color: message.type === 'success' ? 'var(--success)' : 'var(--danger)',
                    border: `1px solid ${message.type === 'success' ? 'rgba(16,185,129,0.2)' : 'rgba(239,68,68,0.2)'}`,
                }}>
                    {message.type === 'success' ? '✓' : '⚠'} {message.text}
                </div>
            )}

            {/* KPIs */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px', marginBottom: '24px' }}>
                {[
                    { label: 'Empresas totales', value: companies.length, color: 'var(--brand-primary)' },
                    { label: 'Empresas activas', value: companies.filter(c => c.isActive).length, color: 'var(--success)' },
                    { label: 'Invitaciones pendientes', value: pendingInvitations.length, color: 'var(--warning)' },
                    { label: 'Invitaciones usadas', value: usedInvitations.filter(i => i.isUsed).length, color: 'var(--text-muted)' },
                ].map(k => (
                    <div key={k.label} className="erp-card" style={{ padding: '16px 20px' }}>
                        <div style={{ fontSize: '24px', fontWeight: 800, color: k.color }}>{k.value}</div>
                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>{k.label}</div>
                    </div>
                ))}
            </div>

            {/* Companies table */}
            <div className="erp-card" style={{ overflow: 'hidden', marginBottom: '24px' }}>
                <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                    <h2 style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-primary)', margin: 0 }}>
                        Empresas registradas
                    </h2>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>{companies.length} empresas</span>
                </div>

                {companies.length === 0 ? (
                    <div style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)', fontSize: '13px' }}>
                        No hay empresas registradas todavía
                    </div>
                ) : (
                    <table className="erp-table">
                        <thead>
                            <tr>
                                {['EMPRESA', 'CIF/NIF', 'PLAN', 'ESTADO', 'PAÍS', 'REGISTRO'].map(h => (
                                    <th key={h}>{h}</th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {companies.map(c => (
                                <tr key={c.id}>
                                    <td style={{ fontWeight: 600, color: 'var(--text-primary)' }}>
                                        {c.name}
                                    </td>
                                    <td>
                                        <code style={{ fontSize: '12px', color: c.taxId.startsWith('PENDING') ? 'var(--warning)' : 'var(--text-secondary)', fontFamily: 'monospace' }}>
                                            {c.taxId.startsWith('PENDING') ? '⏳ Pendiente' : c.taxId}
                                        </code>
                                    </td>
                                    <td>
                                        <span style={{
                                            padding: '2px 10px', borderRadius: '99px', fontSize: '11px', fontWeight: 700,
                                            background: (PLAN_COLORS[c.planName] ?? '#6b7280') + '20',
                                            color: PLAN_COLORS[c.planName] ?? '#6b7280',
                                        }}>
                                            {c.planName}
                                        </span>
                                    </td>
                                    <td>
                                        <span style={{
                                            padding: '2px 10px', borderRadius: '99px', fontSize: '11px', fontWeight: 600,
                                            background: c.isActive ? 'var(--success-bg)' : 'var(--danger-bg)',
                                            color: c.isActive ? 'var(--success)' : 'var(--danger)',
                                        }}>
                                            {c.isActive ? 'Activa' : 'Inactiva'}
                                        </span>
                                    </td>
                                    <td style={{fontSize: '12px', color: 'var(--text-muted)' }}>
                                        {c.country}
                                    </td>
                                    <td style={{fontSize: '12px', color: 'var(--text-muted)' }}>
                                        {new Date(c.createdAt).toLocaleDateString('es-ES')}
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                )}
            </div>

            {/* Pending invitations */}
            {pendingInvitations.length > 0 && (
                <div className="erp-card" style={{ overflow: 'hidden', marginBottom: '24px' }}>
                    <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)' }}>
                        <h2 style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-primary)', margin: 0 }}>
                            Invitaciones pendientes
                        </h2>
                    </div>
                    <table className="erp-table">
                        <thead>
                            <tr>
                                {['EMPRESA', 'EMAIL', 'EXPIRA', 'ENLACE'].map(h => (
                                    <th key={h}>{h}</th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {pendingInvitations.map(inv => {
                                const expiresIn = daysUntil(inv.expiresAt);
                                return (
                                    <tr key={inv.id}>
                                        <td style={{ fontWeight: 600, color: 'var(--text-primary)' }}>
                                            {inv.companyName}
                                        </td>
                                        <td style={{color: 'var(--text-secondary)' }}>
                                            {inv.email}
                                        </td>
                                        <td style={{fontSize: '12px' }}>
                                            <span style={{
                                                color: expiresIn <= 1 ? 'var(--danger)' : expiresIn <= 3 ? 'var(--warning)' : 'var(--text-muted)'
                                            }}>
                                                {expiresIn <= 0 ? 'Hoy' : `${expiresIn}d`}
                                            </span>
                                        </td>
                                        <td>
                                            <button
                                                className="btn btn-secondary btn-sm"
                                                onClick={() => copyInviteLink(inv.token)}
                                                style={{ fontSize: '12px' }}
                                            >
                                                📋 Copiar enlace
                                            </button>
                                        </td>
                                    </tr>
                                );
                            })}
                        </tbody>
                    </table>
                </div>
            )}

            {/* Invitation info note */}
            <div style={{
                background: 'var(--info-bg)', border: '1px solid rgba(59,130,246,0.2)',
                borderRadius: '10px', padding: '16px 20px', fontSize: '13px', color: 'var(--info)',
            }}>
                <strong>ℹ️ Cómo funciona el alta de empresas:</strong>
                <ol style={{ margin: '8px 0 0 0', paddingLeft: '20px', lineHeight: 2 }}>
                    <li>Haz clic en <strong>Invitar empresa</strong> e introduce el nombre y email del administrador</li>
                    <li>Se genera un enlace seguro válido por 7 días</li>
                    <li>Comparte el enlace con el administrador de la empresa</li>
                    <li>El administrador accede al enlace, introduce su CIF y contraseña, y ya tiene acceso al ERP</li>
                </ol>
            </div>

            {/* Modal: New invitation */}
            <AccessibleModal
                open={showModal}
                onClose={() => { setShowModal(false); setInviteResult(null); }}
                title={inviteResult ? 'Invitación generada' : 'Invitar nueva empresa'}
                maxWidth="480px"
            >
                {!inviteResult ? (
                            <>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '14px', marginBottom: '20px' }}>
                                    <div className="form-group">
                                        <label className="erp-label">NOMBRE DE LA EMPRESA *</label>
                                        <input
                                            className="erp-input"
                                            placeholder="Empresa S.L."
                                            value={form.companyName}
                                            onChange={e => setForm({ ...form, companyName: e.target.value })}
                                        />
                                    </div>
                                    <div className="form-group">
                                        <label className="erp-label">EMAIL DEL ADMINISTRADOR *</label>
                                        <input
                                            className="erp-input"
                                            type="email"
                                            placeholder="admin@empresa.com"
                                            value={form.adminEmail}
                                            onChange={e => setForm({ ...form, adminEmail: e.target.value })}
                                        />
                                    </div>
                                </div>

                                {error && (
                                    <div style={{
                                        background: 'var(--danger-bg)', border: '1px solid rgba(239,68,68,0.2)',
                                        borderRadius: '8px', padding: '10px 14px', marginBottom: '16px',
                                        fontSize: '13px', color: 'var(--danger)',
                                    }}>
                                        ⚠️ {error}
                                    </div>
                                )}

                                <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '16px' }}>
                                    La empresa se creará con el plan <strong>Pro (trial 30 días)</strong>.
                                    El administrador recibirá un enlace válido por 7 días para completar el registro.
                                </p>

                                <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                                    <button className="btn btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
                                    <button className="btn btn-primary" onClick={handleInvite} disabled={saving}>
                                        {saving ? 'Generando...' : '✉️ Generar invitación'}
                                    </button>
                                </div>
                            </>
                        ) : (
                            <>
                                <div style={{
                                    background: 'var(--success-bg)', border: '1px solid rgba(16,185,129,0.2)',
                                    borderRadius: '10px', padding: '20px', marginBottom: '20px', textAlign: 'center',
                                }}>
                                    <div style={{ fontSize: '32px', marginBottom: '8px' }}>✅</div>
                                    <div style={{ fontSize: '15px', fontWeight: 700, color: 'var(--success)', marginBottom: '4px' }}>
                                        Invitación generada
                                    </div>
                                    <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                                        Para: <strong>{inviteResult.email}</strong>
                                    </div>
                                </div>

                                <div style={{ marginBottom: '20px' }}>
                                    <label className="erp-label">ENLACE DE REGISTRO (válido 7 días)</label>
                                    <div style={{
                                        background: 'var(--surface-2)', border: '1px solid var(--border)',
                                        borderRadius: '8px', padding: '12px 14px', marginTop: '6px',
                                        display: 'flex', gap: '10px', alignItems: 'center',
                                    }}>
                                        <code style={{ flex: 1, fontSize: '12px', color: 'var(--brand-primary)', wordBreak: 'break-all' }}>
                                            {typeof window !== 'undefined' ? window.location.origin : ''}{inviteResult.inviteUrlPath}
                                        </code>
                                        <button
                                            className="btn btn-secondary btn-sm"
                                            onClick={() => copyInviteLink(inviteResult.token)}
                                            style={{ flexShrink: 0 }}
                                        >
                                            {copied ? '✓ Copiado' : '📋 Copiar'}
                                        </button>
                                    </div>
                                </div>

                                <div style={{
                                    background: 'var(--warning-bg)', border: '1px solid rgba(245,158,11,0.2)',
                                    borderRadius: '8px', padding: '10px 14px', fontSize: '12px', color: '#92400e',
                                    marginBottom: '20px',
                                }}>
                                    ⚠️ Comparte este enlace directamente con el administrador de la empresa. Expira en 7 días.
                                </div>

                                <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                                    <button className="btn btn-primary" onClick={() => { setShowModal(false); setInviteResult(null); }}>
                                        Cerrar
                                    </button>
                                </div>
                            </>
                        )}
            </AccessibleModal>
        </div>
    );
}
