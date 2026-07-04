'use client';
import { useEffect, useState, useMemo } from 'react';
import PageContainer from '@/components/PageContainer';
import AccessibleModal from '@/components/AccessibleModal';
import { parseListResponse } from '@/lib/parseListResponse';
import type { BankAccount } from './page';

type TreasuryTab = 'accounts' | 'movements' | 'effects' | 'orders' | 'forecast' | 'cash';

interface BankMovement {
    id: string; bankAccountId: string; date: string; reference: string;
    description: string; amount: number; type: string; isReconciled: boolean; origin: string;
}
interface CashEffect {
    id: string; effectNumber: string; clientName: string; clientTaxId: string;
    amount: number; issueDate: string; dueDate: string; status: string; bankAccountId?: string;
}
interface PaymentOrder {
    id: string; paymentType: string; beneficiaryName: string; beneficiaryIban: string;
    amount: number; status: string; scheduledDate?: string; executedAt?: string; notes?: string;
}
interface ForecastItem {
    id: string; forecastDate: string; expectedInflow: number; expectedOutflow: number;
    expectedBalance: number; source: string; isActual: boolean; notes?: string;
}
interface CashSession {
    id: string; openedAt: string; openingBalance: number;
    closedAt?: string; expectedClosingBalance?: number; countedClosingBalance?: number;
    difference?: number; status: 'Open' | 'Closed'; notes?: string;
}

const EMPTY_ACCOUNT = { name: '', iban: '', bic: '', bankName: '', notes: '', accountingAccountCode: '' };
const EMPTY_EFFECT = { clientName: '', clientTaxId: '', effectNumber: '', issueDate: '', dueDate: '', amount: '', bankAccountId: '' };
const EMPTY_ORDER = { paymentType: 'Supplier', beneficiaryName: '', beneficiaryTaxId: '', beneficiaryIban: '', description: '', amount: '', scheduledDate: '', bankAccountId: '' };

type TreasuryForm = Partial<typeof EMPTY_ACCOUNT & typeof EMPTY_EFFECT & typeof EMPTY_ORDER & { csv: string }>;

const STATUS_EFFECT: Record<string, string> = {
    Pending: 'badge-warning', Accepted: 'badge-info', Paid: 'badge-success',
    Returned: 'badge-danger', Cancelled: 'badge-gray',
};
const STATUS_ORDER: Record<string, string> = {
    Draft: 'badge-gray', Approved: 'badge-info', Executed: 'badge-success', Cancelled: 'badge-danger',
};

interface TreasuryClientProps {
    initialAccounts: BankAccount[];
}

export default function TreasuryClient({ initialAccounts }: TreasuryClientProps) {
    const [tab, setTab] = useState<TreasuryTab>('accounts');
    const [accounts, setAccounts] = useState<BankAccount[]>(initialAccounts);
    const [selectedAccount, setSelectedAccount] = useState<string>(initialAccounts[0]?.id ?? '');
    const [movements, setMovements] = useState<BankMovement[]>([]);
    const [effects, setEffects] = useState<CashEffect[]>([]);
    const [orders, setOrders] = useState<PaymentOrder[]>([]);
    const [forecast, setForecast] = useState<ForecastItem[]>([]);
    const [loading, setLoading] = useState(false);
    const [showModal, setShowModal] = useState<string | null>(null);
    const [form, setForm] = useState<TreasuryForm>({});
    const [saving, setSaving] = useState(false);
    const [importCsv, setImportCsv] = useState('');
    const [reconciling, setReconciling] = useState(false);
    const [reconcileResult, setReconcileResult] = useState<{ matchedCount: number; matchedAmount: number; message: string } | null>(null);
    const [openCashSessionData, setOpenCashSessionData] = useState<CashSession | null>(null);
    const [cashSessions, setCashSessions] = useState<CashSession[]>([]);
    const [cashOpeningBalance, setCashOpeningBalance] = useState('');
    const [cashCountedBalance, setCashCountedBalance] = useState('');
    const [cashSaving, setCashSaving] = useState(false);
    const [cashMessage, setCashMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

    const fmt = (n: number) => `€ ${(n || 0).toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;
    const fmtDate = (d?: string) => d ? new Date(d).toLocaleDateString('es-ES') : '—';
    const now = new Date();

    const totalBalance = useMemo(
        () => accounts.filter(a => a.isActive).reduce((sum, a) => sum + (a.currentBalance || 0), 0),
        [accounts],
    );
    const activeAccount = useMemo(() => accounts.find(a => a.id === selectedAccount) ?? null, [accounts, selectedAccount]);
    const unreconciledCount = useMemo(() => movements.filter(m => !m.isReconciled).length, [movements]);

    const loadAccounts = async () => {
        const r = await fetch('/api/proxy/treasury/bank-accounts');
        if (r.ok) { const data = await r.json(); setAccounts(data); if (!selectedAccount && data.length) setSelectedAccount(data[0].id); }
    };
    const loadMovements = async (accountId?: string) => {
        const id = accountId || selectedAccount;
        if (!id) return;
        setLoading(true);
        const r = await fetch(`/api/proxy/treasury/bank-accounts/${id}/movements?pageSize=500`);
        if (r.ok) { const d = await r.json(); setMovements(parseListResponse<BankMovement>(d)); }
        setLoading(false);
    };
    const loadEffects = async () => {
        const r = await fetch('/api/proxy/treasury/effects?pageSize=500');
        if (r.ok) { const d = await r.json(); setEffects(parseListResponse<CashEffect>(d)); }
    };
    const loadOrders = async () => {
        const r = await fetch('/api/proxy/treasury/payment-orders?pageSize=500');
        if (r.ok) { const d = await r.json(); setOrders(parseListResponse<PaymentOrder>(d)); }
    };
    const loadForecast = async () => {
        const r = await fetch(`/api/proxy/treasury/forecasts?year=${now.getFullYear()}&month=${now.getMonth() + 1}`);
        if (r.ok) setForecast(await r.json());
    };
    const loadCashSession = async () => {
        const [openRes, allRes] = await Promise.all([
            fetch('/api/proxy/treasury/cash-sessions/open'),
            fetch('/api/proxy/treasury/cash-sessions'),
        ]);
        setOpenCashSessionData(openRes.ok ? await openRes.json() : null);
        if (allRes.ok) setCashSessions(await allRes.json());
    };

    useEffect(() => {
        if (tab === 'movements') loadMovements();
        if (tab === 'effects') loadEffects();
        if (tab === 'orders') loadOrders();
        if (tab === 'forecast') loadForecast();
        if (tab === 'cash') loadCashSession();
    }, [tab, selectedAccount]);

    const openCashSession = async () => {
        setCashSaving(true);
        setCashMessage(null);
        try {
            const res = await fetch('/api/proxy/treasury/cash-sessions/open', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ openingBalance: parseFloat(cashOpeningBalance) || 0 }),
            });
            if (res.ok) {
                setCashOpeningBalance('');
                await loadCashSession();
            } else {
                const err = await res.json().catch(() => ({}));
                setCashMessage({ type: 'error', text: err.error || err.title || 'Error al abrir la caja.' });
            }
        } finally {
            setCashSaving(false);
        }
    };

    const closeCashSession = async () => {
        if (!openCashSessionData) return;
        setCashSaving(true);
        setCashMessage(null);
        try {
            const res = await fetch(`/api/proxy/treasury/cash-sessions/${openCashSessionData.id}/close`, {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ countedClosingBalance: parseFloat(cashCountedBalance) || 0 }),
            });
            if (res.ok) {
                setCashCountedBalance('');
                setCashMessage({ type: 'success', text: 'Caja cerrada correctamente.' });
                await loadCashSession();
            } else {
                const err = await res.json().catch(() => ({}));
                setCashMessage({ type: 'error', text: err.error || err.title || 'Error al cerrar la caja.' });
            }
        } finally {
            setCashSaving(false);
        }
    };

    const saveAccount = async () => {
        setSaving(true);
        const r = await fetch('/api/proxy/treasury/bank-accounts', {
            method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(form),
        });
        setSaving(false);
        if (r.ok) { setShowModal(null); loadAccounts(); }
    };

    const saveEffect = async () => {
        setSaving(true);
        const r = await fetch('/api/proxy/treasury/effects', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ ...form, amount: parseFloat(form.amount) }),
        });
        setSaving(false);
        if (r.ok) { setShowModal(null); loadEffects(); }
    };

    const updateEffectStatus = async (id: string, newStatus: string) => {
        await fetch(`/api/proxy/treasury/effects/${id}/status`, {
            method: 'PATCH', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ newStatus }),
        });
        loadEffects();
    };

    const saveOrder = async () => {
        setSaving(true);
        const r = await fetch('/api/proxy/treasury/payment-orders', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ ...form, amount: parseFloat(form.amount) }),
        });
        setSaving(false);
        if (r.ok) { setShowModal(null); loadOrders(); }
    };

    const importStatement = async () => {
        if (!selectedAccount || !importCsv.trim()) return;
        setSaving(true);
        const r = await fetch(`/api/proxy/treasury/bank-accounts/${selectedAccount}/import`, {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ csvContent: importCsv }),
        });
        setSaving(false);
        if (r.ok) { setShowModal(null); setImportCsv(''); loadMovements(); }
    };

    const reconcile = async () => {
        if (!selectedAccount) return;
        setReconciling(true);
        const r = await fetch(`/api/proxy/treasury/bank-accounts/${selectedAccount}/reconcile`, { method: 'POST' });
        if (r.ok) setReconcileResult(await r.json());
        setReconciling(false);
    };

    const tabs: { key: TreasuryTab; label: string }[] = [
        { key: 'accounts', label: 'Cuentas Bancarias' },
        { key: 'movements', label: 'Movimientos' },
        { key: 'effects', label: 'Efectos Comerciales' },
        { key: 'orders', label: 'Órdenes de Pago' },
        { key: 'forecast', label: 'Previsión de Caja' },
        { key: 'cash', label: 'Arqueo de Caja' },
    ];

    return (
        <PageContainer>
            {/* Header */}
            <div className="page-header">
                <div>
                    <h1 className="page-title">Tesorería</h1>
                    <p className="page-subtitle">Gestión bancaria, cobros y pagos</p>
                </div>
                {tab === 'accounts' && (
                    <button className="btn btn-primary" onClick={() => { setForm(EMPTY_ACCOUNT); setShowModal('account'); }}>
                        + Nueva Cuenta
                    </button>
                )}
                {tab === 'effects' && (
                    <button className="btn btn-primary" onClick={() => { setForm(EMPTY_EFFECT); setShowModal('effect'); }}>
                        + Nuevo Efecto
                    </button>
                )}
                {tab === 'orders' && (
                    <button className="btn btn-primary" onClick={() => { setForm(EMPTY_ORDER); setShowModal('order'); }}>
                        + Nueva Orden
                    </button>
                )}
                {tab === 'movements' && (
                    <div style={{ display: 'flex', gap: '8px' }}>
                        <button className="btn btn-secondary" onClick={() => { setImportCsv(''); setShowModal('import'); }}>⬆ Importar CSV</button>
                        <button className="btn btn-secondary" onClick={reconcile} disabled={reconciling}>
                            {reconciling ? 'Conciliando...' : '⚖ Conciliar'}
                        </button>
                    </div>
                )}
            </div>

            {/* KPI banner */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '24px' }}>
                <div className="erp-card" style={{ padding: '16px 20px' }}>
                    <div style={{ fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Saldo total</div>
                    <div style={{ fontSize: '24px', fontWeight: 900, color: totalBalance >= 0 ? 'var(--success)' : 'var(--danger)', marginTop: '4px' }}>{fmt(totalBalance)}</div>
                </div>
                <div className="erp-card" style={{ padding: '16px 20px' }}>
                    <div style={{ fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Cuentas activas</div>
                    <div style={{ fontSize: '24px', fontWeight: 900, color: 'var(--text-primary)', marginTop: '4px' }}>{accounts.filter(a => a.isActive).length}</div>
                </div>
                <div className="erp-card" style={{ padding: '16px 20px' }}>
                    <div style={{ fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Efectos pendientes</div>
                    <div style={{ fontSize: '24px', fontWeight: 900, color: 'var(--warning)', marginTop: '4px' }}>{effects.filter(e => e.status === 'Pending').length}</div>
                </div>
                <div className="erp-card" style={{ padding: '16px 20px' }}>
                    <div style={{ fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Órdenes pendientes</div>
                    <div style={{ fontSize: '24px', fontWeight: 900, color: 'var(--brand-primary)', marginTop: '4px' }}>{orders.filter(o => o.status === 'Draft' || o.status === 'Approved').length}</div>
                </div>
            </div>

            {/* Reconcile result */}
            {reconcileResult && (
                <div style={{ marginBottom: '16px', padding: '12px 16px', borderRadius: '8px', background: 'var(--success-bg)', color: 'var(--success)', fontSize: '13px', fontWeight: 500, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span>✓ {reconcileResult.message} — {reconcileResult.matchedCount} movimientos · {fmt(reconcileResult.matchedAmount)}</span>
                    <button onClick={() => setReconcileResult(null)} style={{ border: 'none', background: 'none', cursor: 'pointer', color: 'var(--success)', fontSize: '18px' }}>✕</button>
                </div>
            )}

            {/* Tabs */}
            <div style={{ display: 'flex', gap: '4px', marginBottom: '20px', borderBottom: '1px solid var(--border)', paddingBottom: '0' }}>
                {tabs.map(t => (
                    <button key={t.key} onClick={() => setTab(t.key)} style={{
                        padding: '8px 16px', border: 'none', cursor: 'pointer', fontSize: '13px', fontWeight: 600,
                        background: 'none', borderBottom: `2px solid ${tab === t.key ? 'var(--brand-primary)' : 'transparent'}`,
                        color: tab === t.key ? 'var(--brand-primary)' : 'var(--text-secondary)',
                        transition: 'all 0.15s',
                    }}>{t.label}</button>
                ))}
            </div>

            {/* Account selector for movements */}
            {(tab === 'movements') && accounts.length > 0 && (
                <div style={{ marginBottom: '16px', display: 'flex', gap: '12px', alignItems: 'center' }}>
                    <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)' }}>Cuenta:</label>
                    <select className="erp-input" style={{ margin: 0, maxWidth: '320px' }} value={selectedAccount} onChange={e => { setSelectedAccount(e.target.value); loadMovements(e.target.value); }}>
                        {accounts.map(a => <option key={a.id} value={a.id}>{a.name} — {a.iban}</option>)}
                    </select>
                </div>
            )}

            {/* ── TAB: ACCOUNTS ── */}
            {tab === 'accounts' && (
                accounts.length === 0 ? (
                    <div className="erp-card" style={{ padding: '56px', textAlign: 'center' }}>
                        <div style={{ fontSize: '36px', marginBottom: '12px' }}>🏦</div>
                        <p style={{ color: 'var(--text-muted)', fontSize: '14px' }}>No hay cuentas bancarias. Crea la primera.</p>
                    </div>
                ) : (
                    <div className="erp-card" style={{ overflow: 'hidden' }}>
                        <table className="erp-table">
                            <thead><tr>
                                <th>Nombre</th>
                                <th>IBAN</th>
                                <th>Banco</th>
                                <th>Cta. contable</th>
                                <th style={{ textAlign: 'right' }}>Saldo</th>
                                <th>Estado</th>
                            </tr></thead>
                            <tbody>
                                {accounts.map(a => (
                                    <tr key={a.id}>
                                        <td style={{ fontWeight: 600 }}>{a.name}</td>
                                        <td><code style={{ fontSize: '12px', letterSpacing: '0.05em' }}>{a.iban}</code></td>
                                        <td style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>{a.bankName}</td>
                                        <td><code style={{ fontSize: '12px' }}>{a.accountingAccountCode || '572'}</code></td>
                                        <td style={{ textAlign: 'right', fontWeight: 700, color: a.currentBalance >= 0 ? 'var(--success)' : 'var(--danger)', fontSize: '15px' }}>{fmt(a.currentBalance)}</td>
                                        <td><span className={`badge ${a.isActive ? 'badge-success' : 'badge-gray'}`}>{a.isActive ? 'Activa' : 'Inactiva'}</span></td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )
            )}

            {/* ── TAB: MOVEMENTS ── */}
            {tab === 'movements' && (
                loading ? (
                    <div className="erp-card" style={{ padding: '48px', textAlign: 'center', color: 'var(--text-muted)' }}>Cargando movimientos...</div>
                ) : movements.length === 0 ? (
                    <div className="erp-card" style={{ padding: '56px', textAlign: 'center' }}>
                        <div style={{ fontSize: '36px', marginBottom: '12px' }}>📋</div>
                        <p style={{ color: 'var(--text-muted)', fontSize: '14px' }}>Sin movimientos. Importa un extracto CSV.</p>
                    </div>
                ) : (
                    <div className="erp-card" style={{ overflow: 'hidden' }}>
                        <table className="erp-table">
                            <thead><tr>
                                <th>Fecha</th>
                                <th>Descripción</th>
                                <th>Referencia</th>
                                <th style={{ textAlign: 'right' }}>Importe</th>
                                <th>Origen</th>
                                <th>Conciliado</th>
                            </tr></thead>
                            <tbody>
                                {movements.map(m => (
                                    <tr key={m.id}>
                                        <td style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>{fmtDate(m.date)}</td>
                                        <td style={{ fontSize: '13px' }}>{m.description}</td>
                                        <td style={{ fontSize: '12px', fontFamily: 'monospace', color: 'var(--text-muted)' }}>{m.reference || '—'}</td>
                                        <td style={{ textAlign: 'right', fontWeight: 700, color: m.amount >= 0 ? 'var(--success)' : 'var(--danger)' }}>
                                            {m.amount >= 0 ? '+' : ''}{fmt(m.amount)}
                                        </td>
                                        <td><span style={{ fontSize: '11px', padding: '2px 6px', borderRadius: '4px', background: 'var(--surface-2)', color: 'var(--text-muted)' }}>{m.origin}</span></td>
                                        <td>
                                            {m.isReconciled
                                                ? <span className="badge badge-success">✓ Sí</span>
                                                : <span className="badge badge-gray">Pendiente</span>}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )
            )}

            {/* ── TAB: EFFECTS ── */}
            {tab === 'effects' && (
                effects.length === 0 ? (
                    <div className="erp-card" style={{ padding: '56px', textAlign: 'center' }}>
                        <div style={{ fontSize: '36px', marginBottom: '12px' }}>📄</div>
                        <p style={{ color: 'var(--text-muted)', fontSize: '14px' }}>No hay efectos comerciales.</p>
                    </div>
                ) : (
                    <div className="erp-card" style={{ overflow: 'hidden' }}>
                        <table className="erp-table">
                            <thead><tr>
                                <th>Nº Efecto</th>
                                <th>Cliente</th>
                                <th>Emisión</th>
                                <th>Vencimiento</th>
                                <th style={{ textAlign: 'right' }}>Importe</th>
                                <th>Estado</th>
                                <th style={{ textAlign: 'right' }}>Acción</th>
                            </tr></thead>
                            <tbody>
                                {effects.map(e => {
                                    const overdue = new Date(e.dueDate) < now && e.status === 'Pending';
                                    return (
                                        <tr key={e.id}>
                                            <td style={{ fontFamily: 'monospace', fontSize: '12px' }}>{e.effectNumber}</td>
                                            <td style={{ fontWeight: 600 }}>{e.clientName}<div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{e.clientTaxId}</div></td>
                                            <td style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>{fmtDate(e.issueDate)}</td>
                                            <td style={{ fontSize: '13px', fontWeight: overdue ? 700 : 400, color: overdue ? 'var(--danger)' : 'var(--text-primary)' }}>{fmtDate(e.dueDate)}{overdue && <span className="badge badge-danger" style={{ marginLeft: 6 }}>Vencido</span>}</td>
                                            <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(e.amount)}</td>
                                            <td><span className={`badge ${STATUS_EFFECT[e.status] || 'badge-gray'}`}>{e.status}</span></td>
                                            <td style={{ textAlign: 'right' }}>
                                                {e.status === 'Pending' && (
                                                    <div style={{ display: 'flex', gap: '6px', justifyContent: 'flex-end' }}>
                                                        <button className="btn btn-secondary btn-sm" onClick={() => updateEffectStatus(e.id, 'Accepted')}>Aceptar</button>
                                                        <button className="btn btn-secondary btn-sm" onClick={() => updateEffectStatus(e.id, 'Paid')}>Cobrado</button>
                                                        <button className="btn btn-sm" style={{ background: 'var(--danger-bg)', color: 'var(--danger)' }} onClick={() => updateEffectStatus(e.id, 'Returned')}>Devuelto</button>
                                                    </div>
                                                )}
                                                {e.status === 'Accepted' && (
                                                    <button className="btn btn-secondary btn-sm" onClick={() => updateEffectStatus(e.id, 'Paid')}>Marcar Cobrado</button>
                                                )}
                                            </td>
                                        </tr>
                                    );
                                })}
                            </tbody>
                        </table>
                    </div>
                )
            )}

            {/* ── TAB: PAYMENT ORDERS ── */}
            {tab === 'orders' && (
                orders.length === 0 ? (
                    <div className="erp-card" style={{ padding: '56px', textAlign: 'center' }}>
                        <div style={{ fontSize: '36px', marginBottom: '12px' }}>💸</div>
                        <p style={{ color: 'var(--text-muted)', fontSize: '14px' }}>No hay órdenes de pago.</p>
                    </div>
                ) : (
                    <div className="erp-card" style={{ overflow: 'hidden' }}>
                        <table className="erp-table">
                            <thead><tr>
                                <th>Beneficiario</th>
                                <th>Tipo</th>
                                <th>IBAN destino</th>
                                <th>Fecha prevista</th>
                                <th style={{ textAlign: 'right' }}>Importe</th>
                                <th>Estado</th>
                            </tr></thead>
                            <tbody>
                                {orders.map(o => (
                                    <tr key={o.id}>
                                        <td style={{ fontWeight: 600 }}>{o.beneficiaryName}</td>
                                        <td><span style={{ fontSize: '11px', padding: '2px 8px', borderRadius: '4px', background: 'var(--surface-2)', color: 'var(--text-secondary)' }}>{o.paymentType}</span></td>
                                        <td><code style={{ fontSize: '12px' }}>{o.beneficiaryIban}</code></td>
                                        <td style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>{fmtDate(o.scheduledDate)}</td>
                                        <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(o.amount)}</td>
                                        <td><span className={`badge ${STATUS_ORDER[o.status] || 'badge-gray'}`}>{o.status}</span></td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )
            )}

            {/* ── TAB: FORECAST ── */}
            {tab === 'forecast' && (
                forecast.length === 0 ? (
                    <div className="erp-card" style={{ padding: '56px', textAlign: 'center' }}>
                        <div style={{ fontSize: '36px', marginBottom: '12px' }}>📈</div>
                        <p style={{ color: 'var(--text-muted)', fontSize: '14px' }}>Sin previsiones para este período.</p>
                    </div>
                ) : (
                    <div className="erp-card" style={{ overflow: 'hidden' }}>
                        <table className="erp-table">
                            <thead><tr>
                                <th>Fecha</th>
                                <th style={{ textAlign: 'right', color: 'var(--success)' }}>Entradas</th>
                                <th style={{ textAlign: 'right', color: 'var(--danger)' }}>Salidas</th>
                                <th style={{ textAlign: 'right' }}>Saldo previsto</th>
                                <th>Origen</th>
                                <th>Tipo</th>
                            </tr></thead>
                            <tbody>
                                {forecast.map(f => (
                                    <tr key={f.id}>
                                        <td style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>{fmtDate(f.forecastDate)}</td>
                                        <td style={{ textAlign: 'right', fontWeight: 600, color: 'var(--success)' }}>{f.expectedInflow > 0 ? fmt(f.expectedInflow) : '—'}</td>
                                        <td style={{ textAlign: 'right', fontWeight: 600, color: 'var(--danger)' }}>{f.expectedOutflow > 0 ? fmt(f.expectedOutflow) : '—'}</td>
                                        <td style={{ textAlign: 'right', fontWeight: 800, color: f.expectedBalance >= 0 ? 'var(--success)' : 'var(--danger)' }}>{fmt(f.expectedBalance)}</td>
                                        <td style={{ fontSize: '12px', color: 'var(--text-muted)' }}>{f.source}</td>
                                        <td><span className={`badge ${f.isActual ? 'badge-success' : 'badge-info'}`}>{f.isActual ? 'Real' : 'Previsto'}</span></td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )
            )}

            {/* ── TAB: CASH SESSIONS (Arqueo de Caja) ── */}
            {tab === 'cash' && (
                <div>
                    {cashMessage && (
                        <div style={{
                            marginBottom: '16px', padding: '12px 16px', borderRadius: '6px',
                            background: cashMessage.type === 'success' ? 'rgba(34,197,94,0.1)' : 'rgba(239,68,68,0.1)',
                            border: `1px solid ${cashMessage.type === 'success' ? '#22c55e' : '#ef4444'}`,
                            color: cashMessage.type === 'success' ? '#15803d' : '#991b1b',
                            fontSize: '13px',
                        }}>
                            {cashMessage.text}
                        </div>
                    )}

                    <div className="erp-card" style={{ padding: '20px', marginBottom: '20px' }}>
                        {!openCashSessionData ? (
                            <>
                                <h2 style={{ fontSize: '16px', fontWeight: 700, marginBottom: '12px' }}>Abrir caja</h2>
                                <div style={{ display: 'flex', gap: '12px', alignItems: 'flex-end' }}>
                                    <div style={{ flex: 1 }}>
                                        <label className="erp-label">IMPORTE DE APERTURA</label>
                                        <input type="number" step="0.01" className="erp-input" value={cashOpeningBalance}
                                            onChange={e => setCashOpeningBalance(e.target.value)} placeholder="0.00" />
                                    </div>
                                    <button className="btn btn-primary" onClick={openCashSession} disabled={cashSaving}>
                                        {cashSaving ? 'Abriendo...' : '🔓 Abrir Caja'}
                                    </button>
                                </div>
                            </>
                        ) : (
                            <>
                                <h2 style={{ fontSize: '16px', fontWeight: 700, marginBottom: '4px' }}>Caja abierta</h2>
                                <p style={{ fontSize: '13px', color: 'var(--text-muted)', marginBottom: '12px' }}>
                                    Abierta el {fmtDate(openCashSessionData.openedAt)} con {fmt(openCashSessionData.openingBalance)} de apertura.
                                </p>
                                <div style={{ display: 'flex', gap: '12px', alignItems: 'flex-end' }}>
                                    <div style={{ flex: 1 }}>
                                        <label className="erp-label">IMPORTE CONTADO AL CIERRE</label>
                                        <input type="number" step="0.01" className="erp-input" value={cashCountedBalance}
                                            onChange={e => setCashCountedBalance(e.target.value)} placeholder="0.00" />
                                    </div>
                                    <button className="btn btn-primary" onClick={closeCashSession} disabled={cashSaving}>
                                        {cashSaving ? 'Cerrando...' : '🔒 Cerrar Caja'}
                                    </button>
                                </div>
                            </>
                        )}
                    </div>

                    {cashSessions.length === 0 ? (
                        <div className="erp-card" style={{ padding: '56px', textAlign: 'center' }}>
                            <div style={{ fontSize: '36px', marginBottom: '12px' }}>🧾</div>
                            <p style={{ color: 'var(--text-muted)', fontSize: '14px' }}>Sin arqueos de caja todavía.</p>
                        </div>
                    ) : (
                        <div className="erp-card" style={{ overflow: 'hidden' }}>
                            <table className="erp-table">
                                <thead><tr>
                                    <th>Apertura</th>
                                    <th style={{ textAlign: 'right' }}>Importe apertura</th>
                                    <th>Cierre</th>
                                    <th style={{ textAlign: 'right' }}>Esperado</th>
                                    <th style={{ textAlign: 'right' }}>Contado</th>
                                    <th style={{ textAlign: 'right' }}>Diferencia</th>
                                    <th>Estado</th>
                                </tr></thead>
                                <tbody>
                                    {cashSessions.map(s => (
                                        <tr key={s.id}>
                                            <td style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>{fmtDate(s.openedAt)}</td>
                                            <td style={{ textAlign: 'right' }}>{fmt(s.openingBalance)}</td>
                                            <td style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>{s.closedAt ? fmtDate(s.closedAt) : '—'}</td>
                                            <td style={{ textAlign: 'right' }}>{s.expectedClosingBalance != null ? fmt(s.expectedClosingBalance) : '—'}</td>
                                            <td style={{ textAlign: 'right' }}>{s.countedClosingBalance != null ? fmt(s.countedClosingBalance) : '—'}</td>
                                            <td style={{ textAlign: 'right', fontWeight: 700, color: !s.difference ? 'var(--text-secondary)' : s.difference > 0 ? 'var(--success)' : 'var(--danger)' }}>
                                                {s.difference != null ? fmt(s.difference) : '—'}
                                            </td>
                                            <td><span className={`badge ${s.status === 'Open' ? 'badge-info' : 'badge-gray'}`}>{s.status === 'Open' ? 'Abierta' : 'Cerrada'}</span></td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}
                </div>
            )}

            {/* ── MODAL: Nueva cuenta ── */}
            <AccessibleModal open={showModal === 'account'} onClose={() => setShowModal(null)} title="Nueva Cuenta Bancaria" maxWidth="520px"
                footer={(<div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                    <button className="btn btn-secondary" onClick={() => setShowModal(null)}>Cancelar</button>
                    <button className="btn btn-primary" onClick={saveAccount} disabled={saving}>{saving ? 'Guardando...' : '✓ Crear Cuenta'}</button>
                </div>)}>
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '20px' }}>
                            <div className="form-group" style={{ gridColumn: 'span 2' }}>
                                <label className="erp-label">NOMBRE *</label>
                                <input className="erp-input" value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} placeholder="Cuenta Principal BBVA" />
                            </div>
                            <div className="form-group" style={{ gridColumn: 'span 2' }}>
                                <label className="erp-label">IBAN *</label>
                                <input className="erp-input" value={form.iban} onChange={e => setForm({ ...form, iban: e.target.value })} placeholder="ES91 2100 0418 4502 0005 1332" />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">BANCO *</label>
                                <input className="erp-input" value={form.bankName} onChange={e => setForm({ ...form, bankName: e.target.value })} placeholder="BBVA" />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">BIC / SWIFT</label>
                                <input className="erp-input" value={form.bic} onChange={e => setForm({ ...form, bic: e.target.value })} placeholder="BBVAESMMXXX" />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">CÓDIGO CONTABLE PGC</label>
                                <input className="erp-input" value={form.accountingAccountCode} onChange={e => setForm({ ...form, accountingAccountCode: e.target.value })} placeholder="572 (banco), 5721 (TPV), 5722 (Bizum)" />
                            </div>
                            <div className="form-group" style={{ gridColumn: 'span 2' }}>
                                <label className="erp-label">NOTAS</label>
                                <input className="erp-input" value={form.notes} onChange={e => setForm({ ...form, notes: e.target.value })} placeholder="Observaciones opcionales" />
                            </div>
                        </div>
            </AccessibleModal>

            <AccessibleModal open={showModal === 'effect'} onClose={() => setShowModal(null)} title="Nuevo Efecto Comercial" maxWidth="520px"
                footer={(<div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                    <button className="btn btn-secondary" onClick={() => setShowModal(null)}>Cancelar</button>
                    <button className="btn btn-primary" onClick={saveEffect} disabled={saving}>{saving ? 'Guardando...' : '✓ Crear Efecto'}</button>
                </div>)}>
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '20px' }}>
                            <div className="form-group"><label className="erp-label">CLIENTE *</label><input className="erp-input" value={form.clientName} onChange={e => setForm({ ...form, clientName: e.target.value })} placeholder="Empresa SA" /></div>
                            <div className="form-group"><label className="erp-label">CIF / NIF *</label><input className="erp-input" value={form.clientTaxId} onChange={e => setForm({ ...form, clientTaxId: e.target.value })} placeholder="B12345678" /></div>
                            <div className="form-group"><label className="erp-label">Nº EFECTO *</label><input className="erp-input" value={form.effectNumber} onChange={e => setForm({ ...form, effectNumber: e.target.value })} placeholder="EF-2026-001" /></div>
                            <div className="form-group"><label className="erp-label">IMPORTE *</label><input className="erp-input" type="number" value={form.amount} onChange={e => setForm({ ...form, amount: e.target.value })} placeholder="1500.00" /></div>
                            <div className="form-group"><label className="erp-label">FECHA EMISIÓN *</label><input className="erp-input" type="date" value={form.issueDate} onChange={e => setForm({ ...form, issueDate: e.target.value })} /></div>
                            <div className="form-group"><label className="erp-label">FECHA VENCIMIENTO *</label><input className="erp-input" type="date" value={form.dueDate} onChange={e => setForm({ ...form, dueDate: e.target.value })} /></div>
                            <div className="form-group" style={{ gridColumn: 'span 2' }}>
                                <label className="erp-label">CUENTA DE COBRO</label>
                                <select className="erp-input" value={form.bankAccountId} onChange={e => setForm({ ...form, bankAccountId: e.target.value })}>
                                    <option value="">— Sin asignar —</option>
                                    {accounts.map(a => <option key={a.id} value={a.id}>{a.name} — {a.iban}</option>)}
                                </select>
                            </div>
                        </div>
            </AccessibleModal>

            <AccessibleModal open={showModal === 'order'} onClose={() => setShowModal(null)} title="Nueva Orden de Pago" maxWidth="520px"
                footer={(<div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                    <button className="btn btn-secondary" onClick={() => setShowModal(null)}>Cancelar</button>
                    <button className="btn btn-primary" onClick={saveOrder} disabled={saving}>{saving ? 'Guardando...' : '✓ Crear Orden'}</button>
                </div>)}>
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '20px' }}>
                            <div className="form-group"><label className="erp-label">TIPO *</label>
                                <select className="erp-input" value={form.paymentType} onChange={e => setForm({ ...form, paymentType: e.target.value })}>
                                    <option value="Supplier">Proveedor</option>
                                    <option value="Tax">Impuestos</option>
                                    <option value="Payroll">Nóminas</option>
                                    <option value="Other">Otro</option>
                                </select>
                            </div>
                            <div className="form-group"><label className="erp-label">IMPORTE *</label><input className="erp-input" type="number" value={form.amount} onChange={e => setForm({ ...form, amount: e.target.value })} placeholder="0.00" /></div>
                            <div className="form-group" style={{ gridColumn: 'span 2' }}><label className="erp-label">BENEFICIARIO *</label><input className="erp-input" value={form.beneficiaryName} onChange={e => setForm({ ...form, beneficiaryName: e.target.value })} placeholder="Proveedor SL" /></div>
                            <div className="form-group"><label className="erp-label">NIF BENEFICIARIO</label><input className="erp-input" value={form.beneficiaryTaxId} onChange={e => setForm({ ...form, beneficiaryTaxId: e.target.value })} placeholder="B12345678" /></div>
                            <div className="form-group"><label className="erp-label">FECHA PREVISTA</label><input className="erp-input" type="date" value={form.scheduledDate} onChange={e => setForm({ ...form, scheduledDate: e.target.value })} /></div>
                            <div className="form-group" style={{ gridColumn: 'span 2' }}><label className="erp-label">IBAN DESTINO *</label><input className="erp-input" value={form.beneficiaryIban} onChange={e => setForm({ ...form, beneficiaryIban: e.target.value })} placeholder="ES91 2100..." /></div>
                            <div className="form-group" style={{ gridColumn: 'span 2' }}><label className="erp-label">CONCEPTO *</label><input className="erp-input" value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} placeholder="Factura FRA-2026-001" /></div>
                            <div className="form-group" style={{ gridColumn: 'span 2' }}>
                                <label className="erp-label">CUENTA ORIGEN</label>
                                <select className="erp-input" value={form.bankAccountId} onChange={e => setForm({ ...form, bankAccountId: e.target.value })}>
                                    <option value="">— Sin asignar —</option>
                                    {accounts.map(a => <option key={a.id} value={a.id}>{a.name} — {a.iban}</option>)}
                                </select>
                            </div>
                        </div>
            </AccessibleModal>

            <AccessibleModal open={showModal === 'import'} onClose={() => setShowModal(null)} title="Importar Extracto CSV" maxWidth="560px"
                footer={(<div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                    <button className="btn btn-secondary" onClick={() => setShowModal(null)}>Cancelar</button>
                    <button className="btn btn-primary" onClick={importStatement} disabled={saving || !importCsv.trim()}>{saving ? 'Importando...' : '⬆ Importar'}</button>
                </div>)}>
                        <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '12px' }}>
                            Formato CSV: <code>Date,Amount,Concept,Reference</code><br />
                            Ejemplo: <code>2026-04-01,-1250.00,Pago proveedor,REF-001</code>
                        </p>
                        <textarea
                            className="erp-input"
                            style={{ width: '100%', minHeight: '160px', fontFamily: 'monospace', fontSize: '12px', resize: 'vertical' }}
                            value={importCsv}
                            onChange={e => setImportCsv(e.target.value)}
                            placeholder={"Date,Amount,Concept,Reference\n2026-04-01,5000.00,Cobro factura FAC-001,REF-001\n2026-04-02,-1200.00,Pago proveedor,REF-002"}
                        />
            </AccessibleModal>
        </PageContainer>
    );
}
