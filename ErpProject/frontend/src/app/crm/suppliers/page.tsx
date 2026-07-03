'use client';
import { useEffect, useState } from 'react';
import { parseListResponse } from '@/lib/parseListResponse';

interface Supplier {
    id: string; name: string; taxId: string; email: string; phone: string;
    address: string; isActive: boolean; createdAt: string; bankAccount?: string;
}
interface Activity { id: string; action: string; description: string; timestamp: string; }
interface SupplierDetail {
    supplier: Supplier;
    activities: Activity[];
    expenses: { id: string; fileName: string; totalAmount: number; status: string; documentDate: string }[];
}

const EMPTY_FORM = { name: '', taxId: '', email: '', phone: '', address: '', bankAccount: '' };

export default function SuppliersPage() {
    const [suppliers, setSuppliers] = useState<Supplier[]>([]);
    const [selected, setSelected] = useState<SupplierDetail | null>(null);
    const [loading, setLoading] = useState(true);
    const [loadingDetail, setLoadingDetail] = useState(false);
    const [showModal, setShowModal] = useState(false);
    const [showDetail, setShowDetail] = useState(false);
    const [form, setForm] = useState(EMPTY_FORM);
    const [saving, setSaving] = useState(false);
    const [search, setSearch] = useState('');

    const load = async () => {
        const r = await fetch('/api/proxy/suppliers?pageSize=500');
        if (r.ok) setSuppliers(parseListResponse<Supplier>(await r.json()));
        setLoading(false);
    };
    useEffect(() => { load(); }, []);

    const selectSupplier = async (id: string) => {
        setLoadingDetail(true);
        setShowDetail(true);
        const r = await fetch('/api/proxy/suppliers/' + id);
        if (r.ok) setSelected(await r.json());
        setLoadingDetail(false);
    };

    const save = async () => {
        setSaving(true);
        const r = await fetch('/api/proxy/suppliers', {
            method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(form),
        });
        setSaving(false);
        if (r.ok) { setShowModal(false); setForm(EMPTY_FORM); load(); }
    };

    const filtered = suppliers.filter(s =>
        s.name.toLowerCase().includes(search.toLowerCase()) ||
        s.taxId.toLowerCase().includes(search.toLowerCase())
    );

    const statusBadge = (active: boolean) => (
        <span className={active ? 'badge badge-success' : 'badge badge-danger'}>
            {active ? 'Activo' : 'Inactivo'}
        </span>
    );

    const expenseBadge = (status: string) => {
        const map: Record<string, [string, string]> = {
            Approved: ['var(--success-bg)', 'var(--success)'],
            Pending:  ['#fffbeb', '#b45309'],
            Rejected: ['var(--danger-bg)', 'var(--danger)'],
        };
        const [bg, color] = map[status] ?? ['var(--bg-secondary)', 'var(--text-muted)'];
        return <span style={{ fontSize: '11px', fontWeight: 600, padding: '2px 8px', borderRadius: '99px', background: bg, color }}>{status}</span>;
    };

    return (
        <div style={{ padding: '28px 32px', fontFamily: 'Inter, sans-serif' }}>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Proveedores</h1>
                    <p className="page-subtitle">{suppliers.length} proveedores registrados</p>
                </div>
                <button className="btn btn-primary" onClick={() => { setForm(EMPTY_FORM); setShowModal(true); }}>
                    <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                        <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
                    </svg>
                    Nuevo Proveedor
                </button>
            </div>

            {/* Search */}
            <div style={{ marginBottom: '16px', maxWidth: '320px' }}>
                <input className="erp-input" placeholder="Buscar por nombre o CIF..." value={search}
                    onChange={e => setSearch(e.target.value)} style={{ margin: 0 }} />
            </div>

            {/* Table */}
            {loading ? (
                <div className="erp-card" style={{ padding: '48px', textAlign: 'center', color: 'var(--text-muted)', fontSize: '13px' }}>Cargando...</div>
            ) : filtered.length === 0 ? (
                <div className="erp-card" style={{ padding: '56px', textAlign: 'center' }}>
                    <div style={{ fontSize: '36px', marginBottom: '12px' }}>🏢</div>
                    <p style={{ color: 'var(--text-muted)', fontSize: '14px' }}>
                        {search ? 'Sin resultados para esa búsqueda' : 'No hay proveedores registrados'}
                    </p>
                </div>
            ) : (
                <div className="erp-card" style={{ overflow: 'hidden' }}>
                    <table className="erp-table">
                        <thead>
                            <tr>
                                <th>Nombre</th>
                                <th>CIF / NIF</th>
                                <th>Email</th>
                                <th>Teléfono</th>
                                <th>Estado</th>
                                <th style={{ textAlign: 'right' }}>Acciones</th>
                            </tr>
                        </thead>
                        <tbody>
                            {filtered.map(s => (
                                <tr key={s.id}>
                                    <td style={{ fontWeight: 600 }}>{s.name}</td>
                                    <td style={{ fontSize: '13px', color: 'var(--text-muted)' }}>{s.taxId}</td>
                                    <td style={{ fontSize: '13px', color: 'var(--text-muted)' }}>{s.email || '—'}</td>
                                    <td style={{ fontSize: '13px', color: 'var(--text-muted)' }}>{s.phone || '—'}</td>
                                    <td>{statusBadge(s.isActive)}</td>
                                    <td style={{ textAlign: 'right' }}>
                                        <button className="btn btn-secondary btn-sm" onClick={() => selectSupplier(s.id)}>
                                            Ver ficha
                                        </button>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            {/* Detail Modal */}
            {showDetail && (
                <div className="modal-overlay" onClick={e => { if (e.target === e.currentTarget) setShowDetail(false); }}>
                    <div className="modal-box" style={{ maxWidth: '700px', maxHeight: '85vh', display: 'flex', flexDirection: 'column' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
                            <h2 style={{ fontSize: '18px', fontWeight: 700 }}>
                                {loadingDetail ? 'Cargando...' : selected?.supplier.name}
                            </h2>
                            <button onClick={() => setShowDetail(false)} style={{ border: 'none', background: 'none', cursor: 'pointer', fontSize: '20px', color: 'var(--text-muted)' }}>✕</button>
                        </div>

                        {loadingDetail ? (
                            <div style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)', fontSize: '13px' }}>Cargando ficha...</div>
                        ) : selected && (
                            <div style={{ overflowY: 'auto', flex: 1, display: 'flex', flexDirection: 'column', gap: '20px' }}>
                                {/* Info */}
                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                                    <p style={{ fontSize: '12px', color: 'var(--text-muted)', margin: 0 }}>
                                        Desde {new Date(selected.supplier.createdAt).toLocaleDateString('es-ES')}
                                    </p>
                                    {statusBadge(selected.supplier.isActive)}
                                </div>
                                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px' }}>
                                    {(['CIF / NIF', 'Email', 'Teléfono', 'Dirección'] as string[]).map((label, i) => {
                                        const vals = [selected.supplier.taxId, selected.supplier.email || '—', selected.supplier.phone || '—', selected.supplier.address || '—'];
                                        return (
                                            <div key={label}>
                                                <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', letterSpacing: '0.05em', marginBottom: '3px' }}>{label.toUpperCase()}</div>
                                                <div style={{ fontSize: '13px', color: 'var(--text-primary)' }}>{vals[i]}</div>
                                            </div>
                                        );
                                    })}
                                </div>

                                {/* Timeline */}
                                <div>
                                    <h3 style={{ fontSize: '13px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '12px' }}>Actividad reciente</h3>
                                    {!selected.activities?.length ? (
                                        <p style={{ color: 'var(--text-muted)', fontSize: '13px' }}>Sin actividad registrada</p>
                                    ) : (
                                        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                                            {selected.activities.map(a => (
                                                <div key={a.id} style={{ display: 'flex', justifyContent: 'space-between', paddingBottom: '8px', borderBottom: '1px solid var(--border)' }}>
                                                    <div>
                                                        <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-primary)' }}>{a.action}</span>
                                                        {a.description && <span style={{ fontSize: '12px', color: 'var(--text-muted)', marginLeft: '8px' }}>{a.description}</span>}
                                                    </div>
                                                    <span style={{ fontSize: '11px', color: 'var(--text-muted)', whiteSpace: 'nowrap', marginLeft: '12px' }}>
                                                        {new Date(a.timestamp).toLocaleString('es-ES', { dateStyle: 'short', timeStyle: 'short' })}
                                                    </span>
                                                </div>
                                            ))}
                                        </div>
                                    )}
                                </div>

                                {/* Expenses */}
                                <div>
                                    <h3 style={{ fontSize: '13px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '12px' }}>Gastos / Facturas asociadas</h3>
                                    {!selected.expenses?.length ? (
                                        <p style={{ color: 'var(--text-muted)', fontSize: '13px' }}>Sin gastos asociados</p>
                                    ) : (
                                        <table className="erp-table">
                                            <thead>
                                                <tr>
                                                    <th>Archivo</th>
                                                    <th style={{ textAlign: 'right' }}>Total</th>
                                                    <th>Estado</th>
                                                    <th>Fecha</th>
                                                </tr>
                                            </thead>
                                            <tbody>
                                                {selected.expenses.map(e => (
                                                    <tr key={e.id}>
                                                        <td>{e.fileName}</td>
                                                        <td style={{ textAlign: 'right', fontWeight: 600 }}>
                                                            {e.totalAmount != null ? '€ ' + e.totalAmount.toFixed(2) : '—'}
                                                        </td>
                                                        <td>{expenseBadge(e.status)}</td>
                                                        <td style={{ color: 'var(--text-muted)' }}>
                                                            {e.documentDate ? new Date(e.documentDate).toLocaleDateString('es-ES') : '—'}
                                                        </td>
                                                    </tr>
                                                ))}
                                            </tbody>
                                        </table>
                                    )}
                                </div>
                            </div>
                        )}
                    </div>
                </div>
            )}

            {/* New Supplier Modal */}
            {showModal && (
                <div className="modal-overlay" onClick={e => { if (e.target === e.currentTarget) setShowModal(false); }}>
                    <div className="modal-box" style={{ maxWidth: '520px' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
                            <h2 style={{ fontSize: '18px', fontWeight: 700, color: 'var(--text-primary)' }}>Nuevo Proveedor</h2>
                            <button onClick={() => setShowModal(false)} style={{ border: 'none', background: 'none', cursor: 'pointer', fontSize: '20px', color: 'var(--text-muted)' }}>&times;</button>
                        </div>
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '20px' }}>
                            <div className="form-group" style={{ gridColumn: 'span 2' }}>
                                <label className="erp-label">NOMBRE *</label>
                                <input className="erp-input" placeholder="Empresa Proveedora SL" value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">CIF / NIF *</label>
                                <input className="erp-input" placeholder="B12345678" value={form.taxId} onChange={e => setForm({ ...form, taxId: e.target.value })} />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">EMAIL</label>
                                <input className="erp-input" type="email" placeholder="proveedor@empresa.com" value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">TELÉFONO</label>
                                <input className="erp-input" placeholder="+34 xxx xx xx xx" value={form.phone} onChange={e => setForm({ ...form, phone: e.target.value })} />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">CUENTA BANCARIA</label>
                                <input className="erp-input" placeholder="ES00 0000 0000 00 0000000000" value={form.bankAccount} onChange={e => setForm({ ...form, bankAccount: e.target.value })} />
                            </div>
                            <div className="form-group" style={{ gridColumn: 'span 2' }}>
                                <label className="erp-label">DIRECCIÓN</label>
                                <input className="erp-input" placeholder="Calle, numero, ciudad" value={form.address} onChange={e => setForm({ ...form, address: e.target.value })} />
                            </div>
                        </div>
                        <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                            <button className="btn btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
                            <button className="btn btn-primary" onClick={save} disabled={saving}>
                                {saving ? 'Guardando...' : '✓ Guardar Proveedor'}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
