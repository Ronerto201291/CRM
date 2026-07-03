'use client';
import { useState, useCallback } from 'react';
import PageContainer from '@/components/PageContainer';
import AccessibleModal from '@/components/AccessibleModal';
import { parseListResponse } from '@/lib/parseListResponse';
import { useCachedApi } from '@/hooks/useCachedApi';

interface Alert {
    id: string;
    title: string;
    description?: string;
    scheduledAt: string;
    clientId?: string;
    clientName?: string;
    isAcknowledged: boolean;
    acknowledgedAt?: string;
    snoozedUntil?: string;
    createdAt: string;
}

interface Client { id: string; name: string; }

const EMPTY_FORM = { title: '', description: '', scheduledAt: '', clientId: '' };

interface AlertsClientProps {
    initialAlerts: Alert[];
    initialClients: Client[];
}

export default function AlertsClient({ initialAlerts, initialClients }: AlertsClientProps) {
    const [alerts, setAlerts] = useState<Alert[]>(initialAlerts);
    const [clients, setClients] = useState<Client[]>(initialClients);
    const [loading, setLoading] = useState(false);
    const [showModal, setShowModal] = useState(false);
    const [editing, setEditing] = useState<Alert | null>(null);
    const [form, setForm] = useState(EMPTY_FORM);
    const [saving, setSaving] = useState(false);
    const [filterTab, setFilterTab] = useState<'pending' | 'all'>('pending');
    const { fetchCached, invalidateCached } = useCachedApi();

    const load = useCallback(async () => {
        setLoading(true);
        const [r1, clientsData] = await Promise.all([
            fetch('/api/proxy/crm/alerts'),
            fetchCached<unknown>('clients?pageSize=500'),
        ]);
        if (r1.ok) setAlerts(await r1.json());
        if (clientsData) setClients(parseListResponse<Client>(clientsData));
        setLoading(false);
    }, [fetchCached]);

    const openNew = () => {
        setEditing(null);
        // default to now+1h rounded to next 15min
        const d = new Date(Date.now() + 60 * 60_000);
        d.setMinutes(Math.ceil(d.getMinutes() / 15) * 15, 0, 0);
        setForm({ ...EMPTY_FORM, scheduledAt: toLocalInput(d) });
        setShowModal(true);
    };

    const openEdit = (a: Alert) => {
        setEditing(a);
        setForm({
            title: a.title,
            description: a.description ?? '',
            scheduledAt: toLocalInput(new Date(a.scheduledAt)),
            clientId: a.clientId ?? '',
        });
        setShowModal(true);
    };

    const handleSave = async () => {
        if (!form.title.trim() || !form.scheduledAt) return;
        setSaving(true);
        const body = {
            title: form.title,
            description: form.description || null,
            scheduledAt: new Date(form.scheduledAt).toISOString(),
            clientId: form.clientId || null,
        };
        const url = editing ? `/api/proxy/crm/alerts/${editing.id}` : '/api/proxy/crm/alerts';
        const method = editing ? 'PUT' : 'POST';
        const res = await fetch(url, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
        setSaving(false);
        if (res.ok) { setShowModal(false); load(); }
    };

    const handleAck = async (id: string) => {
        await fetch(`/api/proxy/crm/alerts/${id}/acknowledge`, { method: 'PATCH' });
        load();
    };

    const handleDelete = async (id: string) => {
        if (!confirm('¿Eliminar esta alerta?')) return;
        await fetch(`/api/proxy/crm/alerts/${id}`, { method: 'DELETE' });
        load();
    };

    const displayed = filterTab === 'pending'
        ? alerts.filter(a => !a.isAcknowledged)
        : alerts;

    const pendingCount = alerts.filter(a => !a.isAcknowledged).length;

    const fmtDate = (iso: string) =>
        new Date(iso).toLocaleString('es-ES', { dateStyle: 'short', timeStyle: 'short' });

    const isOverdue = (a: Alert) =>
        !a.isAcknowledged && new Date(a.snoozedUntil ?? a.scheduledAt) < new Date();

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Alertas programadas</h1>
                    <p className="page-subtitle">Recordatorios y avisos con fecha y hora</p>
                </div>
                <button className="btn btn-primary" onClick={openNew}>
                    <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}><path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" /></svg>
                    Nueva alerta
                </button>
            </div>

            {/* KPIs */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px', marginBottom: '24px' }}>
                {[
                    { label: 'Total',     value: alerts.length,                                          color: 'var(--text-primary)' },
                    { label: 'Pendientes', value: pendingCount,                                          color: pendingCount > 0 ? 'var(--warning, #f59e0b)' : 'var(--success)' },
                    { label: 'Vencidas',  value: alerts.filter(isOverdue).length,                       color: alerts.filter(isOverdue).length > 0 ? 'var(--danger)' : 'var(--success)' },
                ].map(k => (
                    <div key={k.label} className="erp-card" style={{ padding: '18px 20px' }}>
                        <div style={{ fontSize: '28px', fontWeight: 800, color: k.color }}>{k.value}</div>
                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>{k.label}</div>
                    </div>
                ))}
            </div>

            {/* Tabs */}
            <div style={{ display: 'flex', gap: '4px', marginBottom: '16px' }}>
                {(['pending', 'all'] as const).map(t => (
                    <button key={t} onClick={() => setFilterTab(t)} style={{
                        padding: '6px 16px', borderRadius: '99px', border: 'none', cursor: 'pointer', fontSize: '13px', fontWeight: 600,
                        background: filterTab === t ? 'var(--brand-primary)' : 'var(--bg-secondary)',
                        color: filterTab === t ? '#fff' : 'var(--text-muted)',
                    }}>
                        {t === 'pending' ? `Pendientes (${pendingCount})` : 'Todas'}
                    </button>
                ))}
            </div>

            {/* Table */}
            {loading ? (
                <div className="erp-card" style={{ padding: '48px', textAlign: 'center', color: 'var(--text-muted)', fontSize: '13px' }}>Cargando...</div>
            ) : displayed.length === 0 ? (
                <div className="erp-card" style={{ padding: '56px', textAlign: 'center' }}>
                    <div style={{ fontSize: '36px', marginBottom: '12px' }}>🔔</div>
                    <p style={{ color: 'var(--text-muted)', fontSize: '14px' }}>
                        {filterTab === 'pending' ? 'No hay alertas pendientes' : 'No hay alertas creadas'}
                    </p>
                </div>
            ) : (
                <div className="erp-card" style={{ overflow: 'hidden' }}>
                    <table className="erp-table">
                        <thead>
                            <tr>
                                <th>Título</th>
                                <th>Programada para</th>
                                <th>Cliente</th>
                                <th>Estado</th>
                                <th style={{ textAlign: 'right' }}>Acciones</th>
                            </tr>
                        </thead>
                        <tbody>
                            {displayed.map(a => (
                                <tr key={a.id} style={{ opacity: a.isAcknowledged ? 0.55 : 1 }}>
                                    <td>
                                        <div style={{ fontWeight: 600 }}>{a.title}</div>
                                        {a.description && <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>{a.description}</div>}
                                    </td>
                                    <td style={{ fontSize: '13px' }}>
                                        <div style={{ fontWeight: isOverdue(a) ? 700 : 400, color: isOverdue(a) ? 'var(--danger)' : 'var(--text-primary)' }}>
                                            {fmtDate(a.scheduledAt)}
                                        </div>
                                        {a.snoozedUntil && !a.isAcknowledged && (
                                            <div style={{ fontSize: '11px', color: 'var(--warning, #f59e0b)', marginTop: '2px' }}>
                                                Pospuesta → {fmtDate(a.snoozedUntil)}
                                            </div>
                                        )}
                                    </td>
                                    <td style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                                        {a.clientName ?? '—'}
                                    </td>
                                    <td>
                                        {a.isAcknowledged
                                            ? <span className="badge badge-success">Aceptada</span>
                                            : isOverdue(a)
                                                ? <span className="badge badge-danger">Vencida</span>
                                                : <span className="badge badge-warning">Pendiente</span>
                                        }
                                    </td>
                                    <td style={{ textAlign: 'right' }}>
                                        <div style={{ display: 'flex', gap: '6px', justifyContent: 'flex-end' }}>
                                            {!a.isAcknowledged && (
                                                <button className="btn btn-secondary btn-sm" onClick={() => handleAck(a.id)}>✓ Aceptar</button>
                                            )}
                                            <button className="btn btn-secondary btn-sm" onClick={() => openEdit(a)}>Editar</button>
                                            <button className="btn btn-sm" style={{ background: 'var(--danger-bg)', color: 'var(--danger)' }} onClick={() => handleDelete(a.id)}>Eliminar</button>
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            {/* Modal */}
            <AccessibleModal
                open={showModal}
                onClose={() => setShowModal(false)}
                title={editing ? 'Editar alerta' : 'Nueva alerta'}
                maxWidth="480px"
                footer={(
                    <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                        <button className="btn btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
                        <button className="btn btn-primary" onClick={handleSave} disabled={saving || !form.title.trim() || !form.scheduledAt}>
                            {saving ? 'Guardando...' : '✓ Guardar'}
                        </button>
                    </div>
                )}
            >

                        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginBottom: '20px' }}>
                            <div className="form-group">
                                <label className="erp-label">TÍTULO *</label>
                                <input className="erp-input" placeholder="Ej: Llamar a cliente" value={form.title}
                                    onChange={e => setForm(f => ({ ...f, title: e.target.value }))} />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">DESCRIPCIÓN (opcional)</label>
                                <textarea className="erp-input" placeholder="Detalles adicionales..." value={form.description}
                                    onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
                                    rows={2} style={{ resize: 'vertical' }} />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">FECHA Y HORA *</label>
                                <input className="erp-input" type="datetime-local" value={form.scheduledAt}
                                    onChange={e => setForm(f => ({ ...f, scheduledAt: e.target.value }))} />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">CLIENTE (opcional)</label>
                                <select className="erp-input" value={form.clientId}
                                    onChange={e => setForm(f => ({ ...f, clientId: e.target.value }))}>
                                    <option value="">Sin cliente asociado</option>
                                    {clients.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                                </select>
                            </div>
                        </div>

            </AccessibleModal>
        </PageContainer>
    );
}

function toLocalInput(d: Date): string {
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
