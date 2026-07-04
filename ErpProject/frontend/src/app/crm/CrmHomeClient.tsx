'use client';
import { useState, useCallback, useMemo } from 'react';
import { parseListResponse, parseTotalCount } from '@/lib/parseListResponse';
import Link from 'next/link';
import PageContainer from '@/components/PageContainer';
import NotesPanel from '@/components/NotesPanel';
import AccessibleModal from '@/components/AccessibleModal';
import FormLabel from '@/components/FormLabel';
import { useCachedApi } from '@/hooks/useCachedApi';
import type { Client, Supplier, Contact } from './page';

type Tab = 'clients' | 'suppliers' | 'contacts';

type CrmEntity = Client | Supplier | Contact;
type CrmForm = Partial<Client & Supplier & Contact>;

interface CrmHomeClientProps {
    initialClients: Client[];
    initialSuppliers: Supplier[];
    initialContacts: Contact[];
    initialProspectsCount: number;
}

export default function CrmHomeClient({ initialClients, initialSuppliers, initialContacts, initialProspectsCount }: CrmHomeClientProps) {
    const [tab, setTab] = useState<Tab>('clients');
    const [clients, setClients] = useState<Client[]>(initialClients);
    const [suppliers, setSuppliers] = useState<Supplier[]>(initialSuppliers);
    const [contacts, setContacts] = useState<Contact[]>(initialContacts);
    const [prospectsCount, setProspectsCount] = useState(initialProspectsCount);
    const [showModal, setShowModal] = useState(false);
    const [editing, setEditing] = useState<CrmEntity | null>(null);
    const [notesTarget, setNotesTarget] = useState<{ id: string; name: string } | null>(null);
    const [search, setSearch] = useState('');
    const [form, setForm] = useState<CrmForm>({});
    const [formError, setFormError] = useState<string | null>(null);
    const { fetchCached, invalidateCached } = useCachedApi();

    const load = useCallback(async () => {
        const [d1, d2, d3, d4] = await Promise.all([
            fetchCached<unknown>('/api/proxy/clients?pageSize=500'),
            fetchCached<unknown>('/api/proxy/suppliers?pageSize=500'),
            fetchCached<unknown>('/api/proxy/contacts?pageSize=500'),
            fetchCached<unknown>('/api/proxy/leads?pageSize=500'),
        ]);
        if (d1) setClients(parseListResponse<Client>(d1));
        if (d2) setSuppliers(parseListResponse<Supplier>(d2));
        if (d3) setContacts(parseListResponse<Contact>(d3));
        if (d4) {
            setProspectsCount(parseTotalCount(d4, parseListResponse(d4).length));
        }
    }, [fetchCached]);

    const getEmptyForm = (t: Tab) => {
        if (t === 'clients') return { name: '', email: '', phone: '', taxId: '', address: '' };
        if (t === 'suppliers') return { name: '', taxId: '', email: '', phone: '', address: '', bankAccount: '' };
        return { name: '', email: '', phone: '', position: '', clientId: '', supplierId: '' };
    };

    const openNew = () => { setEditing(null); setForm(getEmptyForm(tab)); setFormError(null); setShowModal(true); };
    const openEdit = (item: CrmEntity) => { setEditing(item); setForm({ ...item }); setFormError(null); setShowModal(true); };

    const handleSave = async () => {
        setFormError(null);
        if (!form.name?.trim()) {
            setFormError('El nombre es obligatorio');
            return;
        }
        let url = '';
        const method = editing ? 'PUT' : 'POST';
        if (tab === 'clients') url = editing ? `/api/proxy/clients/${editing.id}` : '/api/proxy/clients';
        else if (tab === 'suppliers') url = editing ? `/api/proxy/suppliers/${editing.id}` : '/api/proxy/suppliers';
        else url = editing ? `/api/proxy/contacts/${editing.id}` : '/api/proxy/contacts';

        const body = tab === 'contacts'
            ? { ...form, clientId: form.clientId || null, supplierId: form.supplierId || null }
            : form;

        const res = await fetch(url, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
        if (res.ok) {
            setShowModal(false);
            invalidateCached('/api/proxy/clients');
            invalidateCached('/api/proxy/suppliers');
            invalidateCached('/api/proxy/contacts');
            load();
        }
        else {
            const e = await res.json().catch(() => ({}));
            setFormError(e.error || 'Error al guardar');
        }
    };

    const handleDelete = async (id: string) => {
        if (!confirm('¿Eliminar este registro?')) return;
        const url = tab === 'clients' ? `/api/proxy/clients/${id}` : `/api/proxy/contacts/${id}`;
        await fetch(url, { method: 'DELETE' });
        load();
    };

    const tabLabel: Record<Tab, string> = { clients: 'Cliente', suppliers: 'Proveedor', contacts: 'Contacto' };

    const filteredClients  = useMemo(() => clients.filter(c => !search || c.name.toLowerCase().includes(search.toLowerCase()) || c.taxId?.includes(search)), [clients, search]);
    const filteredSuppliers = useMemo(() => suppliers.filter(s => !search || s.name.toLowerCase().includes(search.toLowerCase()) || s.taxId?.includes(search)), [suppliers, search]);
    const filteredContacts  = useMemo(() => contacts.filter(c => !search || c.name.toLowerCase().includes(search.toLowerCase()) || c.email?.toLowerCase().includes(search.toLowerCase())), [contacts, search]);

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">CRM</h1>
                    <p className="page-subtitle">Clientes · Proveedores · Contactos</p>
                </div>
                <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
                    <div style={{ position: 'relative' }}>
                        <input className="erp-input" placeholder="Buscar..." value={search}
                            onChange={e => setSearch(e.target.value)} style={{ paddingLeft: '32px', width: '200px' }} />
                        <span style={{ position: 'absolute', left: '10px', top: '50%', transform: 'translateY(-50%)', color: 'var(--text-muted)' }}>🔍</span>
                    </div>
                    <button className="btn btn-primary" onClick={openNew}>
                        <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}><path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" /></svg>
                        Nuevo {tabLabel[tab]}
                    </button>
                </div>
            </div>

            {/* Tab selectors with counts */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '14px', marginBottom: '20px' }}>
                {([
                    { key: 'clients',   label: 'Clientes',          count: clients.length,    icon: '👤', color: '#2563eb' },
                    { key: 'suppliers', label: 'Proveedores',        count: suppliers.length,  icon: '🏭', color: '#10b981' },
                    { key: 'contacts',  label: 'Contactos',          count: contacts.length,   icon: '📇', color: '#8b5cf6' },
                ] as const).map(t => (
                    <div key={t.key} className="erp-card"
                        style={{ padding: '16px 20px', cursor: 'pointer', borderColor: tab === t.key ? t.color : 'var(--border)', borderWidth: '2px', transition: 'all 0.15s' }}
                        onClick={() => { setTab(t.key); setSearch(''); }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <div>
                                <div style={{ fontSize: '11px', color: 'var(--text-muted)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '4px' }}>{t.label}</div>
                                <div style={{ fontSize: '28px', fontWeight: 800, color: tab === t.key ? t.color : 'var(--text-primary)' }}>{t.count}</div>
                            </div>
                            <span style={{ fontSize: '24px' }}>{t.icon}</span>
                        </div>
                    </div>
                ))}

                {/* Prospects shortcut card */}
                <Link href="/crm/prospects" style={{ textDecoration: 'none' }}>
                    <div className="erp-card" style={{ padding: '16px 20px', cursor: 'pointer', borderColor: 'var(--border)', borderWidth: '2px', transition: 'all 0.15s', height: '100%' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <div>
                                <div style={{ fontSize: '11px', color: 'var(--text-muted)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '4px' }}>Posibles Clientes</div>
                                <div style={{ fontSize: '28px', fontWeight: 800, color: '#f59e0b' }}>{prospectsCount}</div>
                                <div style={{ fontSize: '10px', color: '#f59e0b', marginTop: '4px', fontWeight: 600 }}>Ver sección →</div>
                            </div>
                            <span style={{ fontSize: '24px' }}>🎯</span>
                        </div>
                    </div>
                </Link>
            </div>

            {/* CLIENTS TABLE */}
            {tab === 'clients' && (
                <div className="erp-card" style={{ overflow: 'hidden' }}>
                    <table className="erp-table">
                        <thead><tr>
                            <th>Nombre</th><th>CIF/NIF</th><th>Email</th><th>Teléfono</th><th>Dirección</th>
                            <th style={{ textAlign: 'right' }}>Acciones</th>
                        </tr></thead>
                        <tbody>
                            {filteredClients.length === 0 && <tr><td colSpan={6}>
                                <div className="empty-state"><div className="empty-state-icon">👤</div>
                                    <div className="empty-state-title">Sin clientes</div>
                                    <div className="empty-state-sub">Crea tu primer cliente o convierte un posible cliente</div></div>
                            </td></tr>}
                            {filteredClients.map(c => (
                                <tr key={c.id}>
                                    <td><span style={{ fontWeight: 600 }}>{c.name}</span></td>
                                    <td><span style={{ fontFamily: 'monospace', fontSize: '12px', color: 'var(--text-secondary)' }}>{c.taxId || '—'}</span></td>
                                    <td style={{ color: 'var(--text-secondary)' }}>{c.email || '—'}</td>
                                    <td style={{ color: 'var(--text-secondary)' }}>{c.phone || '—'}</td>
                                    <td style={{ color: 'var(--text-muted)', fontSize: '12px' }}>{c.address || '—'}</td>
                                    <td style={{ textAlign: 'right' }}>
                                        <div style={{ display: 'flex', gap: '6px', justifyContent: 'flex-end' }}>
                                            <button className="btn btn-secondary btn-sm" onClick={() => setNotesTarget({ id: c.id, name: c.name })}>Notas</button>
                                            <button className="btn btn-secondary btn-sm" onClick={() => openEdit(c)}>Editar</button>
                                            <button className="btn btn-sm" style={{ background: 'var(--danger-bg)', color: 'var(--danger)' }} onClick={() => handleDelete(c.id)}>Eliminar</button>
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            {/* SUPPLIERS TABLE */}
            {tab === 'suppliers' && (
                <div className="erp-card" style={{ overflow: 'hidden' }}>
                    <table className="erp-table">
                        <thead><tr>
                            <th>Proveedor</th><th>CIF</th><th>Email</th><th>Teléfono</th><th>Estado</th>
                            <th style={{ textAlign: 'right' }}>Acciones</th>
                        </tr></thead>
                        <tbody>
                            {filteredSuppliers.length === 0 && <tr><td colSpan={6}>
                                <div className="empty-state"><div className="empty-state-icon">🏭</div>
                                    <div className="empty-state-title">Sin proveedores</div>
                                    <div className="empty-state-sub">Los proveedores se crean automáticamente desde OCR de gastos</div></div>
                            </td></tr>}
                            {filteredSuppliers.map(s => (
                                <tr key={s.id}>
                                    <td><span style={{ fontWeight: 600 }}>{s.name}</span></td>
                                    <td><span style={{ fontFamily: 'monospace', fontSize: '12px', color: 'var(--text-secondary)' }}>{s.taxId || '—'}</span></td>
                                    <td style={{ color: 'var(--text-secondary)' }}>{s.email || '—'}</td>
                                    <td style={{ color: 'var(--text-secondary)' }}>{s.phone || '—'}</td>
                                    <td><span className={`badge ${s.isActive !== false ? 'badge-success' : 'badge-gray'}`}>{s.isActive !== false ? 'Activo' : 'Inactivo'}</span></td>
                                    <td style={{ textAlign: 'right' }}>
                                        <button className="btn btn-secondary btn-sm" onClick={() => openEdit(s)}>Editar</button>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            {/* CONTACTS TABLE */}
            {tab === 'contacts' && (
                <div className="erp-card" style={{ overflow: 'hidden' }}>
                    <table className="erp-table">
                        <thead><tr>
                            <th>Nombre</th><th>Cargo</th><th>Email</th><th>Teléfono</th>
                            <th>Vinculado a</th><th style={{ textAlign: 'right' }}>Acciones</th>
                        </tr></thead>
                        <tbody>
                            {filteredContacts.length === 0 && <tr><td colSpan={6}>
                                <div className="empty-state"><div className="empty-state-icon">📇</div>
                                    <div className="empty-state-title">Sin contactos</div>
                                    <div className="empty-state-sub">Los contactos se pueden asociar a clientes y proveedores</div></div>
                            </td></tr>}
                            {filteredContacts.map(c => (
                                <tr key={c.id}>
                                    <td><span style={{ fontWeight: 600 }}>{c.name}</span></td>
                                    <td style={{ color: 'var(--text-secondary)' }}>{c.position || '—'}</td>
                                    <td style={{ color: 'var(--text-secondary)' }}>{c.email || '—'}</td>
                                    <td style={{ color: 'var(--text-secondary)' }}>{c.phone || '—'}</td>
                                    <td>
                                        <div style={{ display: 'flex', flexDirection: 'column', gap: '3px' }}>
                                            {c.clientName && <span className="badge badge-info" style={{ width: 'fit-content' }}>👤 {c.clientName}</span>}
                                            {c.supplierName && <span className="badge badge-success" style={{ width: 'fit-content' }}>🏭 {c.supplierName}</span>}
                                            {!c.clientName && !c.supplierName && <span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>Sin asociar</span>}
                                        </div>
                                    </td>
                                    <td style={{ textAlign: 'right' }}>
                                        <div style={{ display: 'flex', gap: '6px', justifyContent: 'flex-end' }}>
                                            <button className="btn btn-secondary btn-sm" onClick={() => openEdit(c)}>Editar</button>
                                            <button className="btn btn-sm" style={{ background: 'var(--danger-bg)', color: 'var(--danger)' }} onClick={() => handleDelete(c.id)}>Eliminar</button>
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            {/* MODAL — Notes */}
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
                        <NotesPanel entityType="Client" entityId={notesTarget.id} />
                    </div>
                </div>
            )}

            {/* MODAL — New / Edit */}
            <AccessibleModal
                open={showModal}
                onClose={() => setShowModal(false)}
                title={`${editing ? 'Editar' : 'Nuevo'} ${tabLabel[tab]}`}
                footer={(
                    <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                        <button className="btn btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
                        <button className="btn btn-primary" onClick={handleSave}>✓ Guardar</button>
                    </div>
                )}
            >

                        {formError && (
                            <div style={{ padding: '10px 12px', marginBottom: 14, borderRadius: 8, color: 'var(--danger)', background: 'var(--danger-bg)', fontSize: 13 }}>
                                {formError}
                            </div>
                        )}

                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '20px' }}>
                            {/* CLIENTS FORM */}
                            {tab === 'clients' && <>
                                <div className="form-group" style={{ gridColumn: 'span 2' }}>
                                    <FormLabel htmlFor="crm-client-name" required>NOMBRE</FormLabel>
                                    <input id="crm-client-name" className="erp-input" value={form.name || ''} onChange={e => setForm({ ...form, name: e.target.value })} placeholder="Empresa S.L." />
                                </div>
                                <div className="form-group"><label className="erp-label">CIF/NIF</label><input className="erp-input" value={form.taxId || ''} onChange={e => setForm({ ...form, taxId: e.target.value })} placeholder="B12345678" /></div>
                                <div className="form-group"><label className="erp-label">EMAIL</label><input className="erp-input" type="email" value={form.email || ''} onChange={e => setForm({ ...form, email: e.target.value })} /></div>
                                <div className="form-group"><label className="erp-label">TELÉFONO</label><input className="erp-input" value={form.phone || ''} onChange={e => setForm({ ...form, phone: e.target.value })} /></div>
                                <div className="form-group"><label className="erp-label">DIRECCIÓN</label><input className="erp-input" value={form.address || ''} onChange={e => setForm({ ...form, address: e.target.value })} /></div>
                            </>}
                            {/* SUPPLIERS FORM */}
                            {tab === 'suppliers' && <>
                                <div className="form-group" style={{ gridColumn: 'span 2' }}><label className="erp-label">NOMBRE *</label><input className="erp-input" value={form.name || ''} onChange={e => setForm({ ...form, name: e.target.value })} /></div>
                                <div className="form-group"><label className="erp-label">CIF</label><input className="erp-input" value={form.taxId || ''} onChange={e => setForm({ ...form, taxId: e.target.value })} /></div>
                                <div className="form-group"><label className="erp-label">EMAIL</label><input className="erp-input" type="email" value={form.email || ''} onChange={e => setForm({ ...form, email: e.target.value })} /></div>
                                <div className="form-group"><label className="erp-label">TELÉFONO</label><input className="erp-input" value={form.phone || ''} onChange={e => setForm({ ...form, phone: e.target.value })} /></div>
                                <div className="form-group"><label className="erp-label">CUENTA BANCARIA</label><input className="erp-input" value={form.bankAccount || ''} onChange={e => setForm({ ...form, bankAccount: e.target.value })} placeholder="ES00 0000 0000 0000 0000 0000" /></div>
                            </>}
                            {/* CONTACTS FORM */}
                            {tab === 'contacts' && <>
                                <div className="form-group" style={{ gridColumn: 'span 2' }}><label className="erp-label">NOMBRE *</label><input className="erp-input" value={form.name || ''} onChange={e => setForm({ ...form, name: e.target.value })} placeholder="Nombre completo del contacto" /></div>
                                <div className="form-group"><label className="erp-label">CARGO / POSICIÓN</label><input className="erp-input" value={form.position || ''} onChange={e => setForm({ ...form, position: e.target.value })} placeholder="Director Financiero" /></div>
                                <div className="form-group"><label className="erp-label">EMAIL</label><input className="erp-input" type="email" value={form.email || ''} onChange={e => setForm({ ...form, email: e.target.value })} /></div>
                                <div className="form-group"><label className="erp-label">TELÉFONO</label><input className="erp-input" value={form.phone || ''} onChange={e => setForm({ ...form, phone: e.target.value })} /></div>
                                <div className="form-group">
                                    <label className="erp-label">ASOCIAR A CLIENTE (opcional)</label>
                                    <select className="erp-input" value={form.clientId || ''} onChange={e => setForm({ ...form, clientId: e.target.value || null })}>
                                        <option value="">Sin cliente asociado</option>
                                        {clients.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                                    </select>
                                </div>
                                <div className="form-group">
                                    <label className="erp-label">ASOCIAR A PROVEEDOR (opcional)</label>
                                    <select className="erp-input" value={form.supplierId || ''} onChange={e => setForm({ ...form, supplierId: e.target.value || null })}>
                                        <option value="">Sin proveedor asociado</option>
                                        {suppliers.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
                                    </select>
                                </div>
                            </>}
                        </div>

            </AccessibleModal>
        </PageContainer>
    );
}
