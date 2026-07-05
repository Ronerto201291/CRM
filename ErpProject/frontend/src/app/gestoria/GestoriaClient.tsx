'use client';
import { useEffect, useState } from 'react';
import PageContainer from '@/components/PageContainer';

interface CompanyKpi {
    companyId: string;
    companyName: string;
    roleId?: string;
    roleName?: string;
    adminRoleId?: string;
    contableRoleId?: string;
    monthlyBilling: number;
    monthlyExpenses: number;
    overdueInvoices: number;
    lowStockProducts: number;
    pendingApprovals: number;
    alertCount: number;
}

export default function GestoriaClient() {
    const [companies, setCompanies] = useState<CompanyKpi[]>([]);
    const [loading, setLoading] = useState(true);
    const [savingRole, setSavingRole] = useState<string | null>(null);
    const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

    const loadDashboard = async () => {
        const r = await fetch('/api/proxy/gestoria/dashboard');
        if (r.ok) {
            const data = await r.json();
            setCompanies(Array.isArray(data) ? data : []);
        }
    };

    useEffect(() => {
        queueMicrotask(() => {
            void loadDashboard().finally(() => setLoading(false));
        });
    }, []);

    const showMsg = (type: 'success' | 'error', text: string) => {
        setMessage({ type, text });
        setTimeout(() => setMessage(null), 4000);
    };

    const handleRoleChange = async (companyId: string, roleId: string) => {
        setSavingRole(companyId);
        const r = await fetch(`/api/proxy/gestoria/memberships/${companyId}/role`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ roleId }),
        });
        setSavingRole(null);
        if (r.ok) {
            const company = companies.find(c => c.companyId === companyId);
            const roleName = roleId === company?.adminRoleId ? 'Admin' : roleId === company?.contableRoleId ? 'Contable' : company?.roleName;
            setCompanies(prev => prev.map(c =>
                c.companyId === companyId ? { ...c, roleId, roleName } : c
            ));
            showMsg('success', 'Rol actualizado');
        } else {
            const err = await r.json().catch(() => ({}));
            showMsg('error', err.error || 'Error al actualizar el rol');
        }
    };

    const fmt = (n: number) => `€ ${(n || 0).toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Panel gestoría</h1>
                    <p className="page-subtitle">KPIs agregados por empresa cliente (#42a fases 2–3)</p>
                </div>
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

            {loading ? <p>Cargando...</p> : companies.length === 0 ? (
                <div className="erp-card" style={{ padding: '40px', textAlign: 'center' }}>
                    <p style={{ color: 'var(--text-muted)' }}>Sin empresas vinculadas. Añade empresas en Configuración → Empresas.</p>
                </div>
            ) : (
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))', gap: '16px' }}>
                    {companies.map(c => (
                        <div key={c.companyId} className="erp-card" style={{ padding: '18px' }}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '8px' }}>
                                <strong>{c.companyName}</strong>
                                {c.alertCount > 0 && <span className="badge badge-warning">{c.alertCount} alertas</span>}
                            </div>
                            {(c.adminRoleId || c.contableRoleId) ? (
                                <div style={{ marginBottom: '12px' }}>
                                    <label htmlFor={`role-${c.companyId}`} style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', display: 'block', marginBottom: '4px' }}>
                                        Rol en esta empresa
                                    </label>
                                    <select
                                        id={`role-${c.companyId}`}
                                        className="erp-input"
                                        style={{ fontSize: '13px', padding: '6px 10px' }}
                                        value={c.roleId ?? ''}
                                        disabled={savingRole === c.companyId}
                                        onChange={e => void handleRoleChange(c.companyId, e.target.value)}
                                    >
                                        {c.adminRoleId && <option value={c.adminRoleId}>Admin</option>}
                                        {c.contableRoleId && <option value={c.contableRoleId}>Contable</option>}
                                    </select>
                                </div>
                            ) : c.roleName ? (
                                <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '12px' }}>Rol: {c.roleName}</div>
                            ) : null}
                            <div style={{ fontSize: '13px', display: 'grid', gap: '6px' }}>
                                <div>Facturación mes: <strong>{fmt(c.monthlyBilling)}</strong></div>
                                <div>Gastos mes: <strong>{fmt(c.monthlyExpenses)}</strong></div>
                                <div>Facturas vencidas: {c.overdueInvoices}</div>
                                <div>Stock bajo: {c.lowStockProducts}</div>
                                <div>Aprobaciones pendientes: {c.pendingApprovals}</div>
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </PageContainer>
    );
}
