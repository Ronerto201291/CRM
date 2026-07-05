'use client';
import { useEffect, useState } from 'react';
import PageContainer from '@/components/PageContainer';
import EmptyState from '@/components/EmptyState';

interface Plan { id: string; name: string; monthlyPrice: number; yearlyPrice: number; maxUsers: number; maxInvoicesPerMonth: number; maxCompanies?: number; }
interface Subscription { id: string; planId: string; planName: string; status: string; stripeStatus?: string; currentPeriodStart?: string; currentPeriodEnd?: string; maxCompanies?: number; companiesUsed?: number; }
interface TenantModule { id: string; moduleName: string; isEnabled: boolean; }
interface StripeInvoice { id: string; amount: number; currency: string; status: string; created: string; invoiceUrl?: string; }

interface SubscriptionClientProps {
    initialPlans: Plan[];
    initialSubscription: Subscription | null;
    initialModules: TenantModule[];
    initialStripeInvoices: StripeInvoice[];
}

const MODULE_INFO: Record<string, { icon: string; desc: string }> = {
    CRM: { icon: '👤', desc: 'Clientes, proveedores, contactos, leads' },
    Billing: { icon: '🧾', desc: 'Facturación legal española (RD 1619/2012)' },
    Expenses: { icon: '📸', desc: 'Captura de gastos con OCR + aprobación' },
    Accounting: { icon: '📊', desc: 'Doble partida, IVA, balances' },
    Inventory: { icon: '📦', desc: 'Productos, stock y almacenes' },
    API: { icon: '🔌', desc: 'API pública con ApiKey y rate limit' },
};

export default function SubscriptionClient({
    initialPlans,
    initialSubscription,
    initialModules,
    initialStripeInvoices,
}: SubscriptionClientProps) {
    const [plans, setPlans] = useState<Plan[]>(initialPlans);
    const [subscription, setSubscription] = useState<Subscription | null>(initialSubscription);
    const [modules, setModules] = useState<TenantModule[]>(initialModules);
    const [stripeInvoices, setStripeInvoices] = useState<StripeInvoice[]>(initialStripeInvoices);
    const [billing, setBilling] = useState<'monthly' | 'yearly'>('monthly');
    const [changing, setChanging] = useState(false);

    const load = async () => {
        const [r1, r2, r3, r4] = await Promise.all([
            fetch('/api/proxy/subscription/plans'),
            fetch('/api/proxy/subscription'),
            fetch('/api/proxy/tenant/modules'),
            fetch('/api/proxy/subscription/invoices'),
        ]);
        if (r1.ok) setPlans(await r1.json());
        if (r2.ok) setSubscription(await r2.json());
        if (r3.ok) setModules(await r3.json());
        if (r4.ok) setStripeInvoices(await r4.json());
    };
    useEffect(() => { load(); }, []);

    const toggleModule = async (mod: TenantModule) => {
        await fetch(`/api/proxy/tenant/modules/${mod.id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ isEnabled: !mod.isEnabled }),
        });
        load();
    };

    const changePlan = async (planId: string) => {
        if (!confirm('¿Cambiar al plan seleccionado? Serás redirigido a Stripe para completar el pago.')) return;
        setChanging(true);
        try {
            const r = await fetch('/api/proxy/subscription/checkout', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ planId, billingCycle: billing }),
            });
            if (r.ok) {
                const data = await r.json();
                if (data.sessionUrl) {
                    window.location.href = data.sessionUrl;
                    return;
                }
            }
        } finally {
            setChanging(false);
            load();
        }
    };

    const openPortal = async () => {
        const r = await fetch('/api/proxy/subscription/portal', { method: 'POST' });
        if (r.ok) {
            const data = await r.json();
            if (data.url) window.location.href = data.url;
        }
    };

    const fmtDate = (d?: string) => d ? new Date(d).toLocaleDateString('es-ES') : '—';
    const fmtAmount = (amount: number, currency: string) =>
        `${(amount / 100).toFixed(2)} ${currency.toUpperCase()}`;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Suscripción y Licencias</h1>
                    <p className="page-subtitle">Plan activo · Módulos disponibles · Facturación</p>
                </div>
                <button className="btn btn-secondary" onClick={openPortal}>Gestionar Facturación Stripe</button>
            </div>

            {subscription && (
                <div className="erp-card" style={{ padding: '24px', marginBottom: '24px' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                        <div>
                            <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '4px' }}>Plan Activo</div>
                            <div style={{ fontSize: '22px', fontWeight: 800, color: 'var(--brand-primary)' }}>{subscription.planName}</div>
                            <div style={{ marginTop: '8px', display: 'flex', gap: '16px', fontSize: '13px', color: 'var(--text-secondary)' }}>
                                <span>Inicio: {fmtDate(subscription.currentPeriodStart)}</span>
                                <span>Vence: {fmtDate(subscription.currentPeriodEnd)}</span>
                            </div>
                            {(subscription.maxCompanies ?? 0) > 0 && (
                                <div style={{ marginTop: '10px', fontSize: '13px' }}>
                                    Empresas: <strong>{subscription.companiesUsed ?? 0}</strong> / {subscription.maxCompanies === 9999 ? '∞' : subscription.maxCompanies}
                                </div>
                            )}
                        </div>
                        <span className={`badge ${subscription.status === 'active' ? 'badge-success' : 'badge-warning'}`} style={{ fontSize: '13px', padding: '4px 14px' }}>
                            {subscription.stripeStatus || subscription.status}
                        </span>
                    </div>
                </div>
            )}

            <div className="erp-card" style={{ padding: '24px', marginBottom: '24px' }}>
                <h2 style={{ fontSize: '15px', fontWeight: 700, marginBottom: '16px' }}>Módulos</h2>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '12px' }}>
                    {modules.length === 0 && (
                        <div style={{ gridColumn: '1 / -1', color: 'var(--text-muted)', fontSize: '13px', padding: '20px', textAlign: 'center' }}>
                            Sin módulos configurados para este tenant
                        </div>
                    )}
                    {modules.map(mod => {
                        const info = MODULE_INFO[mod.moduleName] ?? { icon: '⚙️', desc: mod.moduleName };
                        return (
                            <div key={mod.id} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '14px 16px', border: '1px solid var(--border)', borderRadius: '8px', background: mod.isEnabled ? 'var(--success-bg)' : 'var(--surface-2)', transition: 'all 0.15s' }}>
                                <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
                                    <span style={{ fontSize: '20px' }}>{info.icon}</span>
                                    <div>
                                        <div style={{ fontWeight: 700, fontSize: '13px' }}>{mod.moduleName}</div>
                                        <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{info.desc}</div>
                                    </div>
                                </div>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                    <span style={{ fontSize: '11px', color: mod.isEnabled ? 'var(--success)' : 'var(--text-muted)', fontWeight: 600 }}>
                                        {mod.isEnabled ? 'Activo' : 'Inactivo'}
                                    </span>
                                    <div style={{ width: '40px', height: '22px', borderRadius: '99px', background: mod.isEnabled ? 'var(--success)' : 'var(--border)', cursor: 'pointer', position: 'relative', transition: 'background 0.2s' }}
                                        onClick={() => toggleModule(mod)}>
                                        <div style={{ width: '16px', height: '16px', borderRadius: '50%', background: 'white', position: 'absolute', top: '3px', transition: 'left 0.2s', left: mod.isEnabled ? '21px' : '3px', boxShadow: '0 1px 3px rgba(0,0,0,0.2)' }} />
                                    </div>
                                </div>
                            </div>
                        );
                    })}
                </div>
            </div>

            <div className="erp-card" style={{ padding: '24px', marginBottom: '24px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
                    <h2 style={{ fontSize: '15px', fontWeight: 700 }}>Planes Disponibles</h2>
                    <div style={{ display: 'flex', background: 'var(--surface-2)', borderRadius: '8px', padding: '3px', gap: '2px' }}>
                        {(['monthly', 'yearly'] as const).map(b => (
                            <button key={b} onClick={() => setBilling(b)} style={{ padding: '5px 14px', borderRadius: '6px', border: 'none', fontSize: '12px', fontWeight: billing === b ? 700 : 500, background: billing === b ? 'var(--brand-primary)' : 'transparent', color: billing === b ? 'white' : 'var(--text-secondary)', cursor: 'pointer' }}>
                                {b === 'monthly' ? 'Mensual' : 'Anual (-20%)'}
                            </button>
                        ))}
                    </div>
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: `repeat(${Math.max(plans.length, 1)}, 1fr)`, gap: '16px' }}>
                    {plans.length === 0 && (
                        <EmptyState icon="💳" title="Sin planes configurados" />
                    )}
                    {plans.map(plan => {
                        const price = billing === 'monthly' ? plan.monthlyPrice : plan.yearlyPrice;
                        const isCurrent = subscription?.planId === plan.id;
                        return (
                            <div key={plan.id} style={{ padding: '24px', border: `2px solid ${isCurrent ? 'var(--brand-primary)' : 'var(--border)'}`, borderRadius: '10px', position: 'relative', background: isCurrent ? 'rgba(37,99,235,0.03)' : 'var(--surface)' }}>
                                {isCurrent && <div style={{ position: 'absolute', top: '-10px', left: '50%', transform: 'translateX(-50%)', background: 'var(--brand-primary)', color: 'white', fontSize: '10px', fontWeight: 700, padding: '2px 12px', borderRadius: '99px', whiteSpace: 'nowrap' }}>PLAN ACTUAL</div>}
                                <div style={{ fontSize: '16px', fontWeight: 800, marginBottom: '8px' }}>{plan.name}</div>
                                <div style={{ fontSize: '28px', fontWeight: 900, color: 'var(--brand-primary)', marginBottom: '4px' }}>
                                    {price === 0 ? 'Gratis' : `€${price}`}<span style={{ fontSize: '13px', fontWeight: 500, color: 'var(--text-muted)' }}>/{billing === 'monthly' ? 'mes' : 'año'}</span>
                                </div>
                                <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginBottom: '20px' }}>
                                    Hasta {plan.maxUsers === 0 ? '∞' : plan.maxUsers} usuarios · {plan.maxInvoicesPerMonth === 0 ? '∞' : plan.maxInvoicesPerMonth} facturas/mes
                                    {(plan.maxCompanies ?? 0) > 1 && <> · hasta {plan.maxCompanies} empresas</>}
                                </div>
                                <button className={`btn ${isCurrent ? 'btn-secondary' : 'btn-primary'}`} style={{ width: '100%' }}
                                    disabled={isCurrent || changing} onClick={() => changePlan(plan.id)}>
                                    {isCurrent ? 'Plan actual' : changing ? '...' : 'Contratar'}
                                </button>
                            </div>
                        );
                    })}
                </div>
            </div>

            {/* Stripe Billing History */}
            {stripeInvoices.length > 0 && (
                <div className="erp-card" style={{ padding: '24px' }}>
                    <h2 style={{ fontSize: '15px', fontWeight: 700, marginBottom: '16px' }}>Historial de Facturación</h2>
                    <table className="erp-table">
                        <thead>
                            <tr>
                                <th>Fecha</th>
                                <th style={{ textAlign: 'right' }}>Importe</th>
                                <th>Estado</th>
                                <th style={{ textAlign: 'right' }}>Factura</th>
                            </tr>
                        </thead>
                        <tbody>
                            {stripeInvoices.map(inv => (
                                <tr key={inv.id}>
                                    <td style={{ fontSize: '13px' }}>{new Date(inv.created).toLocaleDateString('es-ES')}</td>
                                    <td style={{ textAlign: 'right', fontWeight: 600 }}>{fmtAmount(inv.amount, inv.currency)}</td>
                                    <td>
                                        <span className={`badge ${inv.status === 'paid' ? 'badge-success' : 'badge-warning'}`}>
                                            {inv.status}
                                        </span>
                                    </td>
                                    <td style={{ textAlign: 'right' }}>
                                        {inv.invoiceUrl && (
                                            <a href={inv.invoiceUrl} target="_blank" rel="noopener noreferrer" className="btn btn-sm btn-secondary">
                                                Descargar PDF
                                            </a>
                                        )}
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}
        </PageContainer>
    );
}
