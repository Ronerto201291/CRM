'use client';
import { useState, useEffect, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import PageContainer from '@/components/PageContainer';
import NotesPanel from '@/components/NotesPanel';

interface Prospect {
    id: string;
    name: string;
    email: string;
    phone: string;
    taxId: string;
    address: string;
    status: string;
    source: string;
    notes: string;
    convertedToClientId: string | null;
    createdAt: string;
}

const STATUS_OPTIONS = ['New', 'Contacted', 'Qualified', 'Negotiation', 'Won', 'Lost'];
const SOURCE_OPTIONS = ['Web', 'Referral', 'Event', 'Cold Call', 'Social Media', 'Otro'];

const STATUS_LABELS: Record<string, string> = {
    New: 'Nuevo', Contacted: 'Contactado', Qualified: 'Cualificado',
    Negotiation: 'Negociación', Won: 'Ganado', Lost: 'Perdido',
};

const STATUS_BADGE: Record<string, string> = {
    New: 'badge-info',
    Contacted: 'badge-purple',
    Qualified: 'badge-warning',
    Negotiation: 'badge-orange',
    Won: 'badge-success',
    Lost: 'badge-danger',
};

const emptyForm = { name: '', email: '', phone: '', taxId: '', address: '', status: 'New', source: 'Web', notes: '' };

export default function ProspectsPage() {
    const router = useRouter();
    const [prospects, setProspects] = useState<Prospect[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState('');
    const [filterStatus, setFilterStatus] = useState('');
    const [showModal, setShowModal] = useState(false);
    const [editing, setEditing] = useState<Prospect | null>(null);
    const [form, setForm] = useState(emptyForm);
    const [saving, setSaving] = useState(false);
    const [convertingId, setConvertingId] = useState<string | null>(null);
    const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
    const [notesTarget, setNotesTarget] = useState<{ id: string; name: string } | null>(null);

    const showMsg = (type: 'success' | 'error', text: string) => {
        setMessage({ type, text });
        setTimeout(() => setMessage(null), 4000);
    };

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const params = new URLSearchParams();
            if (search) params.set('search', search);
            if (filterStatus) params.set('status', filterStatus);
            const res = await fetch(`/api/proxy/leads?${params}`);
            if (res.ok) setProspects(await res.json());
        } finally {
            setLoading(false);
        }
    }, [search, filterStatus]);

    useEffect(() => { load(); }, [load]);

    const openCreate = () => { setEditing(null); setForm(emptyForm); setShowModal(true); };
    const openEdit = (p: Prospect) => {
        setEditing(p);
        setForm({ name: p.name, email: p.email, phone: p.phone, taxId: p.taxId, address: p.address, status: p.status, source: p.source, notes: p.notes });
        setShowModal(true);
    };

    const handleSave = async () => {
        if (!form.name.trim()) { showMsg('error', 'El nombre es obligatorio'); return; }
        setSaving(true);
        try {
            const url = editing ? `/api/proxy/leads/${editing.id}` : '/api/proxy/leads';
            const method = editing ? 'PUT' : 'POST';
            const res = await fetch(url, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(form) });
            if (res.ok) {
                showMsg('success', editing ? 'Posible cliente actualizado' : 'Posible cliente creado');
                setShowModal(false);
                load();
            } else {
                const err = await res.json().catch(() => ({}));
                showMsg('error', err.error || 'Error al guardar');
            }
        } finally {
            setSaving(false);
        }
    };

    const handleDelete = async (id: string, name: string) => {
        if (!confirm(`¿Eliminar el posible cliente "${name}"?`)) return;
        const res = await fetch(`/api/proxy/leads/${id}`, { method: 'DELETE' });
        if (res.ok) { showMsg('success', 'Eliminado correctamente'); load(); }
        else showMsg('error', 'Error al eliminar');
    };

    const handleConvert = async (p: Prospect) => {
        if (!confirm(`¿Convertir "${p.name}" en cliente? Se creará un cliente con sus datos.`)) return;
        setConvertingId(p.id);
        try {
            const res = await fetch(`/api/proxy/leads/${p.id}/convert-to-client`, { method: 'POST' });
            if (res.ok) {
                showMsg('success', `"${p.name}" ahora es un cliente registrado.`);
                load();
            } else if (res.status === 409) {
                const data = await res.json();
                showMsg('error', data.message);
            } else {
                showMsg('error', 'Error en la conversión');
            }
        } finally {
            setConvertingId(null);
        }
    };

    const total     = prospects.length;
    const active    = prospects.filter(p => !['Won', 'Lost'].includes(p.status)).length;
    const won       = prospects.filter(p => p.status === 'Won').length;
    const converted = prospects.filter(p => p.convertedToClientId).length;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Posibles Clientes</h1>
                    <p className="page-subtitle">Prospectos · Pipeline comercial · Conversión a clientes</p>
                </div>
                <button className="btn btn-primary" onClick={openCreate}>
                    <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                        <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
                    </svg>
                    Nuevo posible cliente
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
                    { label: 'Total',       value: total,     color: 'var(--brand-primary)' },
                    { label: 'En proceso',  value: active,    color: 'var(--warning)' },
                    { label: 'Ganados',     value: won,       color: 'var(--success)' },
                    { label: 'Convertidos', value: converted, color: 'var(--info)' },
                ].map(k => (
                    <div key={k.label} className="erp-card" style={{ padding: '18px 20px' }}>
                        <div style={{ fontSize: '28px', fontWeight: 800, color: k.color }}>{k.value}</div>
                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>{k.label}</div>
                    </div>
                ))}
            </div>

            {/* Filters */}
            <div style={{ display: 'flex', gap: '12px', marginBottom: '16px' }}>
                <input
                    className="erp-input"
                    placeholder="Buscar por nombre, email, NIF..."
                    value={search}
                    onChange={e => setSearch(e.target.value)}
                    style={{ flex: 1, maxWidth: '340px' }}
                />
                <select
                    className="erp-input"
                    value={filterStatus}
                    onChange={e => setFilterStatus(e.target.value)}
                    style={{ width: '180px' }}
                >
                    <option value="">Todos los estados</option>
                    {STATUS_OPTIONS.map(s => <option key={s} value={s}>{STATUS_LABELS[s]}</option>)}
                </select>
            </div>

            {/* Table */}
            <div className="erp-card" style={{ overflow: 'hidden' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                    <thead>
                        <tr style={{ borderBottom: '1px solid var(--border)' }}>
                            {['NOMBRE', 'NIF/CIF', 'EMAIL', 'TELÉFONO', 'ESTADO', 'ORIGEN', 'ACCIONES'].map(h => (
                                <th key={h} style={{ padding: '10px 14px', textAlign: 'left', fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)' }}>{h}</th>
                            ))}
                        </tr>
                    </thead>
                    <tbody>
                        {loading ? (
                            <tr><td colSpan={7} style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)', fontSize: '13px' }}>Cargando...</td></tr>
                        ) : prospects.length === 0 ? (
                            <tr><td colSpan={7}>
                                <div className="empty-state">
                                    <div className="empty-state-icon">🔍</div>
                                    <div className="empty-state-title">Sin posibles clientes</div>
                                    <div className="empty-state-sub">{search || filterStatus ? 'No hay resultados con estos filtros' : 'Crea el primer prospecto para empezar tu pipeline'}</div>
                                </div>
                            </td></tr>
                        ) : prospects.map(p => {
                            const isConverted = !!p.convertedToClientId;
                            return (
                                <tr key={p.id} style={{ borderBottom: '1px solid var(--border)' }}>
                                    <td style={{ padding: '12px 14px', fontWeight: 600, color: 'var(--text-primary)' }}>
                                        {p.name}
                                        {isConverted && (
                                            <span className="badge badge-success" style={{ marginLeft: '8px', fontSize: '10px' }}>
                                                Cliente
                                            </span>
                                        )}
                                    </td>
                                    <td style={{ padding: '12px 14px', color: 'var(--text-muted)', fontFamily: 'monospace', fontSize: '12px' }}>{p.taxId || '—'}</td>
                                    <td style={{ padding: '12px 14px', color: 'var(--text-secondary)' }}>{p.email || '—'}</td>
                                    <td style={{ padding: '12px 14px', color: 'var(--text-secondary)' }}>{p.phone || '—'}</td>
                                    <td style={{ padding: '12px 14px' }}>
                                        <span className={`badge ${STATUS_BADGE[p.status] ?? 'badge-gray'}`}>
                                            {STATUS_LABELS[p.status] ?? p.status}
                                        </span>
                                    </td>
                                    <td style={{ padding: '12px 14px', color: 'var(--text-muted)', fontSize: '12px' }}>{p.source || '—'}</td>
                                    <td style={{ padding: '12px 14px' }}>
                                        <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
                                            <button className="btn btn-secondary btn-sm" onClick={() => setNotesTarget({ id: p.id, name: p.name })}>Notas</button>
                                            <button className="btn btn-secondary btn-sm" onClick={() => openEdit(p)}>Editar</button>
                                            {!isConverted && (<>
                                                <button
                                                    className="btn btn-secondary btn-sm"
                                                    onClick={() => router.push('/billing/quotes')}
                                                    style={{ color: 'var(--brand-primary)' }}
                                                >
                                                    + Presupuesto
                                                </button>
                                                <button
                                                    className="btn btn-success btn-sm"
                                                    onClick={() => handleConvert(p)}
                                                    disabled={convertingId === p.id}
                                                >
                                                    {convertingId === p.id ? '...' : 'Convertir'}
                                                </button>
                                            </>)}
                                            {isConverted && (
                                                <button className="btn btn-secondary btn-sm" onClick={() => router.push('/crm')}>
                                                    Ver cliente
                                                </button>
                                            )}
                                            <button className="btn btn-danger btn-sm" onClick={() => handleDelete(p.id, p.name)}>Eliminar</button>
                                        </div>
                                    </td>
                                </tr>
                            );
                        })}
                    </tbody>
                </table>
            </div>

            {/* Notes Modal */}
            {notesTarget && (
                <div className="modal-overlay" onClick={e => { if (e.target === e.currentTarget) setNotesTarget(null); }}>
                    <div className="modal-box" style={{ maxWidth: '560px' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
                            <div>
                                <h2 style={{ fontSize: '18px', fontWeight: 700 }}>Notas</h2>
                                <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>{notesTarget.name}</p>
                            </div>
                            <button onClick={() => setNotesTarget(null)} style={{ border: 'none', background: 'none', cursor: 'pointer', fontSize: '20px', color: 'var(--text-muted)' }}>✕</button>
                        </div>
                        <NotesPanel entityType="Lead" entityId={notesTarget.id} />
                    </div>
                </div>
            )}

            {/* Modal */}
            {showModal && (
                <div className="modal-overlay" onClick={e => { if (e.target === e.currentTarget) setShowModal(false); }}>
                    <div className="modal-box" style={{ maxWidth: '540px' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
                            <h2 style={{ fontSize: '18px', fontWeight: 700, color: 'var(--text-primary)' }}>
                                {editing ? 'Editar posible cliente' : 'Nuevo posible cliente'}
                            </h2>
                            <button onClick={() => setShowModal(false)} style={{ border: 'none', background: 'none', cursor: 'pointer', fontSize: '20px', color: 'var(--text-muted)' }}>✕</button>
                        </div>

                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                            <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                                <label className="erp-label">NOMBRE *</label>
                                <input className="erp-input" value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} placeholder="Nombre completo o empresa" />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">NIF / CIF</label>
                                <input className="erp-input" value={form.taxId} onChange={e => setForm(f => ({ ...f, taxId: e.target.value }))} placeholder="B12345678" />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">EMAIL</label>
                                <input className="erp-input" type="email" value={form.email} onChange={e => setForm(f => ({ ...f, email: e.target.value }))} placeholder="email@ejemplo.com" />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">TELÉFONO</label>
                                <input className="erp-input" value={form.phone} onChange={e => setForm(f => ({ ...f, phone: e.target.value }))} placeholder="+34 600 000 000" />
                            </div>
                            <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                                <label className="erp-label">DIRECCIÓN</label>
                                <input className="erp-input" value={form.address} onChange={e => setForm(f => ({ ...f, address: e.target.value }))} placeholder="Calle, ciudad, código postal" />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">ESTADO</label>
                                <select className="erp-input" value={form.status} onChange={e => setForm(f => ({ ...f, status: e.target.value }))}>
                                    {STATUS_OPTIONS.map(s => <option key={s} value={s}>{STATUS_LABELS[s]}</option>)}
                                </select>
                            </div>
                            <div className="form-group">
                                <label className="erp-label">ORIGEN</label>
                                <select className="erp-input" value={form.source} onChange={e => setForm(f => ({ ...f, source: e.target.value }))}>
                                    {SOURCE_OPTIONS.map(s => <option key={s} value={s}>{s}</option>)}
                                </select>
                            </div>
                            <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                                <label className="erp-label">NOTAS</label>
                                <textarea
                                    className="erp-input"
                                    value={form.notes}
                                    onChange={e => setForm(f => ({ ...f, notes: e.target.value }))}
                                    rows={3}
                                    placeholder="Observaciones, intereses, próximos pasos..."
                                    style={{ resize: 'vertical' }}
                                />
                            </div>
                        </div>

                        <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end', marginTop: '20px' }}>
                            <button className="btn btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
                            <button className="btn btn-primary" onClick={handleSave} disabled={saving}>
                                {saving ? 'Guardando...' : editing ? '✓ Guardar cambios' : '✓ Crear'}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </PageContainer>
    );
}
