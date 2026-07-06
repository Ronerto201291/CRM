'use client';
import { useState, useEffect, useCallback } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import PageContainer from '@/components/PageContainer';
import AccessibleModal from '@/components/AccessibleModal';
import FormLabel from '@/components/FormLabel';
import { expenseCreateSchema, type ExpenseCreateFormValues } from '@/lib/schemas/expenseCreateSchema';
import { useCachedApi } from '@/hooks/useCachedApi';

interface ExpenseDoc {
    id: string; invoiceNumber?: string; supplierName?: string; supplierTaxId?: string;
    issueDate?: string; taxBase?: number; vatRate?: number; vatAmount?: number;
    irpfRate?: number; irpfAmount?: number; total?: number;
    status: string; isValidated: boolean; validatedAt?: string; createdAt: string;
    ocrConfidence?: number;
}
interface Stats { pending: number; approved: number; totalVATSoportado: number; totalBase: number; }
interface AnomalyItem { expenseId: string; invoiceNumber?: string; supplierName?: string; amount: number; type: string; message: string; }
interface CategorySuggestion { accountCode: string; accountName: string; reason: string; confidence: number; }

const STATUS_MAP: Record<string, { label: string; cls: string }> = {
    Draft:    { label: 'Borrador',  cls: 'badge-warning' },
    Reviewed: { label: 'Revisado', cls: 'badge-info' },
    Approved: { label: 'Aprobado', cls: 'badge-success' },
    Rejected: { label: 'Rechazado', cls: 'badge-danger' },
};

const defaultCreateForm: ExpenseCreateFormValues = {
    invoiceNumber: '', supplierName: '', supplierTaxId: '',
    issueDate: new Date().toISOString().split('T')[0],
    taxBase: '', vatRate: '21', vatAmount: '', irpfRate: '', irpfAmount: '', total: '',
};

export default function ExpensesClient({
    initialDocs,
    initialStats,
}: {
    initialDocs: ExpenseDoc[];
    initialStats: Stats | null;
}) {
    const [docs, setDocs] = useState<ExpenseDoc[]>(initialDocs);
    const [stats, setStats] = useState<Stats | null>(initialStats);
    const [editing, setEditing] = useState<ExpenseDoc | null>(null);
    const [filter, setFilter] = useState('all');
    const [showCreateModal, setShowCreateModal] = useState(false);
    const { register, handleSubmit, reset, setValue, getValues, formState: { errors } } = useForm<ExpenseCreateFormValues>({
        resolver: zodResolver(expenseCreateSchema),
        defaultValues: defaultCreateForm,
    });
    const [creating, setCreating] = useState(false);
    const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
    const [anomalies, setAnomalies] = useState<{ outliers: AnomalyItem[]; duplicates: AnomalyItem[]; aiSummary?: string } | null>(null);
    const [categoryHint, setCategoryHint] = useState<CategorySuggestion | null>(null);
    const { fetchCached, invalidateCached } = useCachedApi();

    const loadAnomalies = useCallback(async () => {
        const r = await fetch('/api/proxy/expenses/anomalies');
        if (r.ok) {
            const data = await r.json();
            setAnomalies({ outliers: data.outliers ?? [], duplicates: data.duplicates ?? [], aiSummary: data.aiSummary });
        }
    }, []);

    useEffect(() => {
        queueMicrotask(() => { void loadAnomalies(); });
    }, [loadAnomalies]);

    const suggestCategory = async (supplierTaxId?: string, description?: string) => {
        const params = new URLSearchParams();
        if (supplierTaxId) params.set('supplierTaxId', supplierTaxId);
        if (description) params.set('description', description);
        const r = await fetch(`/api/proxy/expenses/suggest-category?${params}`);
        if (r.ok) setCategoryHint(await r.json());
    };

    const refresh = async () => {
        invalidateCached('expenses');
        const [docsData, statsData] = await Promise.all([
            fetchCached<unknown>('expenses'),
            fetchCached<unknown>('expenses/stats'),
        ]);
        if (docsData) setDocs(Array.isArray(docsData) ? docsData as ExpenseDoc[] : []);
        if (statsData) setStats(statsData as Stats);
    };

    const showMsg = (type: 'success' | 'error', text: string) => {
        setMessage({ type, text });
        setTimeout(() => setMessage(null), 4000);
    };

    const approve = async (id: string) => {
        if (!confirm('¿Aprobar y contabilizar este gasto? Esta acción es IRREVERSIBLE.')) return;
        const r = await fetch(`/api/proxy/expenses/${id}/approve`, { method: 'POST' });
        if (r.ok) { showMsg('success', 'Gasto aprobado y contabilizado'); refresh(); }
        else { const e = await r.json().catch(() => ({})); showMsg('error', e.error || 'Error al aprobar'); }
    };

    const saveEdit = async () => {
        if (!editing) return;
        const r = await fetch(`/api/proxy/expenses/${editing.id}`, {
            method: 'PUT', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(editing)
        });
        if (r.ok) { setEditing(null); refresh(); showMsg('success', 'Gasto actualizado'); }
        else showMsg('error', 'Error al guardar');
    };

    const onCreate = handleSubmit(async (data) => {
        setCreating(true);
        const body = {
            invoiceNumber: data.invoiceNumber || null,
            supplierName: data.supplierName || null,
            supplierTaxId: data.supplierTaxId || null,
            issueDate: data.issueDate || null,
            taxBase: data.taxBase ? parseFloat(data.taxBase) : null,
            vatRate: data.vatRate ? parseFloat(data.vatRate) : null,
            vatAmount: data.vatAmount ? parseFloat(data.vatAmount) : null,
            irpfRate: data.irpfRate ? parseFloat(data.irpfRate) : null,
            irpfAmount: data.irpfAmount ? parseFloat(data.irpfAmount) : null,
            total: data.total ? parseFloat(data.total) : null,
        };
        const r = await fetch('/api/proxy/expenses', {
            method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
        });
        setCreating(false);
        if (r.ok) {
            setShowCreateModal(false);
            reset(defaultCreateForm);
            showMsg('success', 'Gasto registrado correctamente');
            refresh();
        } else {
            const e = await r.json().catch(() => ({}));
            showMsg('error', e.error || 'Error al crear el gasto');
        }
    });

    const handleBaseOrRate = (base: string, rate: string) => {
        const b = parseFloat(base);
        const r = parseFloat(rate);
        if (!isNaN(b) && !isNaN(r)) {
            const vatAmt = (b * r / 100).toFixed(2);
            const total = (b + parseFloat(vatAmt)).toFixed(2);
            setValue('vatAmount', vatAmt);
            setValue('total', total);
        }
    };

    const fmt = (n?: number) => n != null ? `€ ${n.toFixed(2)}` : '—';
    const filtered = filter === 'all' ? docs : filter === 'pending' ? docs.filter(d => !d.isValidated) : docs.filter(d => d.isValidated);

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Gastos</h1>
                    <p className="page-subtitle">Captura OCR vía QR · Registro manual · Contabilización automática</p>
                </div>
                <button className="btn btn-primary" onClick={() => { reset(defaultCreateForm); setShowCreateModal(true); }}>
                    <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                        <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
                    </svg>
                    Registrar gasto
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

            {/* Stats */}
            {stats && (
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '14px', marginBottom: '24px' }}>
                    <div className="erp-card" style={{ padding: '18px 20px' }}>
                        <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '6px' }}>Pendientes de revisar</div>
                        <div style={{ fontSize: '28px', fontWeight: 800, color: stats.pending > 0 ? 'var(--warning)' : 'var(--text-primary)' }}>{stats.pending}</div>
                    </div>
                    <div className="erp-card" style={{ padding: '18px 20px' }}>
                        <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '6px' }}>Aprobados</div>
                        <div style={{ fontSize: '28px', fontWeight: 800, color: 'var(--success)' }}>{stats.approved}</div>
                    </div>
                    <div className="erp-card" style={{ padding: '18px 20px' }}>
                        <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '6px' }}>IVA Soportado Total</div>
                        <div style={{ fontSize: '22px', fontWeight: 800, color: 'var(--brand-primary)' }}>{fmt(stats.totalVATSoportado)}</div>
                    </div>
                    <div className="erp-card" style={{ padding: '18px 20px' }}>
                        <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '6px' }}>Base de Gastos</div>
                        <div style={{ fontSize: '22px', fontWeight: 800, color: 'var(--text-primary)' }}>{fmt(stats.totalBase)}</div>
                    </div>
                </div>
            )}

            {anomalies && (anomalies.outliers.length > 0 || anomalies.duplicates.length > 0) && (
                <div className="erp-card" style={{ padding: '16px', marginBottom: '20px', borderLeft: '4px solid var(--warning)' }}>
                    <div style={{ fontWeight: 700, marginBottom: '8px' }}>⚠ Anomalías detectadas (heurística #40)</div>
                    <ul style={{ margin: 0, paddingLeft: '18px', fontSize: '13px', color: 'var(--text-secondary)' }}>
                        {[...anomalies.outliers, ...anomalies.duplicates].slice(0, 5).map(a => (
                            <li key={`${a.expenseId}-${a.type}`}>{a.message}</li>
                        ))}
                    </ul>
                    {anomalies.aiSummary && (
                        <p style={{ marginTop: '10px', fontSize: '13px', fontStyle: 'italic' }}>{anomalies.aiSummary}</p>
                    )}
                </div>
            )}

            {/* Info banner */}
            <div style={{ background: 'var(--info-bg)', border: '1px solid rgba(59,130,246,0.2)', borderRadius: '8px', padding: '12px 16px', marginBottom: '20px', display: 'flex', gap: '12px', alignItems: 'center', fontSize: '13px', color: 'var(--info)' }}>
                <span>ℹ️</span>
                <span>Puedes registrar gastos <strong>manualmente</strong> con el botón de arriba, o capturarlos automáticamente via <strong>código QR</strong> (OCR Tesseract, procesa cada 30 seg).</span>
                <a href="/settings" style={{ marginLeft: 'auto', fontWeight: 600, color: 'var(--brand-primary)', textDecoration: 'none', whiteSpace: 'nowrap' }}>Ver QR →</a>
            </div>

            {/* Filter */}
            <div style={{ display: 'flex', gap: '8px', marginBottom: '16px' }}>
                {[['all', 'Todos'], ['pending', 'Pendientes'], ['approved', 'Aprobados']].map(([v, l]) => (
                    <button key={v} onClick={() => setFilter(v)} style={{
                        padding: '6px 14px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '12px', fontWeight: filter === v ? 700 : 500,
                        background: filter === v ? 'var(--brand-primary)' : 'var(--surface)', color: filter === v ? 'white' : 'var(--text-secondary)', cursor: 'pointer',
                    }}>{l}</button>
                ))}
            </div>

            {/* Edit modal */}
            {editing && (
                <div className="modal-overlay" onClick={e => { if (e.target === e.currentTarget) setEditing(null); }}>
                    <div className="modal-box">
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
                            <h2 style={{ fontSize: '18px', fontWeight: 700 }}>✏️ Revisar Gasto</h2>
                            <button onClick={() => setEditing(null)} style={{ border: 'none', background: 'none', cursor: 'pointer', fontSize: '20px', color: 'var(--text-muted)' }}>✕</button>
                        </div>
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '12px', marginBottom: '20px' }}>
                            <div className="form-group"><label className="erp-label">Nº FACTURA</label><input className="erp-input" value={editing.invoiceNumber || ''} onChange={e => setEditing({ ...editing, invoiceNumber: e.target.value })} /></div>
                            <div className="form-group"><label className="erp-label">PROVEEDOR</label><input className="erp-input" value={editing.supplierName || ''} onChange={e => setEditing({ ...editing, supplierName: e.target.value })} /></div>
                            <div className="form-group"><label className="erp-label">CIF PROVEEDOR</label><input className="erp-input" value={editing.supplierTaxId || ''} onChange={e => setEditing({ ...editing, supplierTaxId: e.target.value })} /></div>
                            <div className="form-group"><label className="erp-label">FECHA</label><input type="date" className="erp-input" value={editing.issueDate?.split('T')[0] || ''} onChange={e => setEditing({ ...editing, issueDate: e.target.value })} /></div>
                            <div className="form-group"><label className="erp-label">BASE IMPONIBLE</label><input type="number" className="erp-input" value={editing.taxBase || ''} onChange={e => setEditing({ ...editing, taxBase: +e.target.value })} /></div>
                            <div className="form-group">
                                <label className="erp-label">% IVA</label>
                                <select className="erp-input" value={editing.vatRate || 21} onChange={e => setEditing({ ...editing, vatRate: +e.target.value })}>
                                    <option value={21}>21%</option><option value={10}>10%</option><option value={4}>4%</option><option value={0}>Exento</option>
                                </select>
                            </div>
                            <div className="form-group"><label className="erp-label">CUOTA IVA</label><input type="number" className="erp-input" value={editing.vatAmount || ''} onChange={e => setEditing({ ...editing, vatAmount: +e.target.value })} /></div>
                            <div className="form-group"><label className="erp-label">% IRPF</label><input type="number" className="erp-input" value={editing.irpfRate || ''} onChange={e => setEditing({ ...editing, irpfRate: +e.target.value })} placeholder="0" /></div>
                            <div className="form-group"><label className="erp-label">TOTAL</label><input type="number" className="erp-input" value={editing.total || ''} onChange={e => setEditing({ ...editing, total: +e.target.value })} /></div>
                        </div>
                        <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                            <button className="btn btn-secondary" onClick={() => setEditing(null)}>Cancelar</button>
                            <button className="btn btn-primary" onClick={saveEdit}>✓ Guardar Revisión</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Create manual expense modal */}
            <AccessibleModal
                open={showCreateModal}
                onClose={() => setShowCreateModal(false)}
                title="Registrar gasto manualmente"
                maxWidth="560px"
                footer={(
                    <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                        <button type="button" className="btn btn-secondary" onClick={() => setShowCreateModal(false)}>Cancelar</button>
                        <button type="submit" form="expense-create-form" className="btn btn-primary" disabled={creating}>
                            {creating ? 'Registrando...' : '✓ Registrar gasto'}
                        </button>
                    </div>
                )}
            >
                        <form id="expense-create-form" onSubmit={onCreate} style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                            <div className="form-group">
                                <FormLabel htmlFor="exp-invoice">Nº factura</FormLabel>
                                <input id="exp-invoice" className="erp-input" placeholder="FAC-2024-001" {...register('invoiceNumber')} />
                            </div>
                            <div className="form-group">
                                <FormLabel htmlFor="exp-date" required>Fecha</FormLabel>
                                <input id="exp-date" type="date" className="erp-input" {...register('issueDate')} />
                                {errors.issueDate && <p style={{ color: 'var(--danger)', fontSize: 12 }}>{errors.issueDate.message}</p>}
                            </div>
                            <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                                <FormLabel htmlFor="exp-supplier">Proveedor</FormLabel>
                                <input id="exp-supplier" className="erp-input" placeholder="Nombre del proveedor" {...register('supplierName')} />
                                {errors.supplierName && <p style={{ color: 'var(--danger)', fontSize: 12 }}>{errors.supplierName.message}</p>}
                            </div>
                            <div className="form-group">
                                <FormLabel htmlFor="exp-taxId">CIF / NIF proveedor</FormLabel>
                                <input id="exp-taxId" className="erp-input" placeholder="B12345678" {...register('supplierTaxId', {
                                    onBlur: (e) => suggestCategory(e.target.value, getValues('supplierName')),
                                })} />
                            </div>
                            {categoryHint && (
                                <div style={{ gridColumn: '1 / -1', fontSize: '12px', color: 'var(--info)', background: 'var(--info-bg)', padding: '8px 12px', borderRadius: '6px' }}>
                                    💡 Categoría sugerida: <strong>{categoryHint.accountCode}</strong> — {categoryHint.accountName} ({Math.round(categoryHint.confidence * 100)}% · {categoryHint.reason})
                                </div>
                            )}
                            <div className="form-group">
                                <FormLabel htmlFor="exp-vatRate">% IVA</FormLabel>
                                <select id="exp-vatRate" className="erp-input" {...register('vatRate', {
                                    onChange: (e) => handleBaseOrRate(getValues('taxBase') || '', e.target.value),
                                })}>
                                    <option value="21">21%</option>
                                    <option value="10">10%</option>
                                    <option value="4">4%</option>
                                    <option value="0">Exento (0%)</option>
                                </select>
                            </div>
                            <div className="form-group">
                                <FormLabel htmlFor="exp-base">Base imponible (€)</FormLabel>
                                <input id="exp-base" type="number" step="0.01" className="erp-input" placeholder="0.00" {...register('taxBase', {
                                    onChange: (e) => handleBaseOrRate(e.target.value, getValues('vatRate') || '21'),
                                })} />
                            </div>
                            <div className="form-group">
                                <FormLabel htmlFor="exp-vat">Cuota IVA (€)</FormLabel>
                                <input id="exp-vat" type="number" step="0.01" className="erp-input" placeholder="0.00" {...register('vatAmount')} />
                            </div>
                            <div className="form-group">
                                <FormLabel htmlFor="exp-irpf-rate">% IRPF</FormLabel>
                                <input id="exp-irpf-rate" type="number" step="0.01" className="erp-input" placeholder="0" {...register('irpfRate')} />
                            </div>
                            <div className="form-group">
                                <FormLabel htmlFor="exp-irpf">Retención IRPF (€)</FormLabel>
                                <input id="exp-irpf" type="number" step="0.01" className="erp-input" placeholder="0.00" {...register('irpfAmount')} />
                            </div>
                            <div className="form-group">
                                <FormLabel htmlFor="exp-total">Total (€)</FormLabel>
                                <input id="exp-total" type="number" step="0.01" className="erp-input" placeholder="0.00" {...register('total')} />
                            </div>
                        </form>
            </AccessibleModal>

            {/* Documents table */}
            <div className="erp-card" style={{ overflow: 'hidden' }}>
                <table className="erp-table">
                    <thead><tr>
                        <th>Nº Factura</th><th>Proveedor</th><th>Fecha</th>
                        <th style={{ textAlign: 'right' }}>Base</th>
                        <th style={{ textAlign: 'right' }}>IVA</th>
                        <th style={{ textAlign: 'right' }}>Total</th>
                        <th>Origen</th><th>Estado</th><th style={{ textAlign: 'right' }}>Acciones</th>
                    </tr></thead>
                    <tbody>
                        {filtered.length === 0 && (
                            <tr><td colSpan={9}>
                                <div className="empty-state">
                                    <div className="empty-state-icon">🧾</div>
                                    <div className="empty-state-title">Sin gastos</div>
                                    <div className="empty-state-sub">Registra un gasto manualmente o genera un QR en Configuración para capturarlos con OCR</div>
                                </div>
                            </td></tr>
                        )}
                        {filtered.map(d => (
                            <tr key={d.id} style={{ background: d.isValidated ? 'rgba(16,185,129,0.02)' : undefined }}>
                                <td style={{ fontFamily: 'monospace', fontSize: '12px' }}>{d.invoiceNumber || '—'}</td>
                                <td>
                                    <span style={{ fontWeight: 600 }}>{d.supplierName || '—'}</span>
                                    {d.supplierTaxId && <><br /><span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{d.supplierTaxId}</span></>}
                                </td>
                                <td style={{ color: 'var(--text-secondary)' }}>{d.issueDate ? new Date(d.issueDate).toLocaleDateString('es-ES') : '—'}</td>
                                <td style={{ textAlign: 'right' }}>{fmt(d.taxBase)}</td>
                                <td style={{ textAlign: 'right', color: 'var(--text-secondary)' }}>{fmt(d.vatAmount)}</td>
                                <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(d.total)}</td>
                                <td>
                                    {d.ocrConfidence != null ? (
                                        <span className="badge badge-info" title={`OCR ${d.ocrConfidence}%`}>OCR</span>
                                    ) : (
                                        <span className="badge badge-gray">Manual</span>
                                    )}
                                </td>
                                <td><span className={`badge ${STATUS_MAP[d.status]?.cls ?? 'badge-gray'}`}>{STATUS_MAP[d.status]?.label ?? d.status}</span></td>
                                <td style={{ textAlign: 'right' }}>
                                    <div style={{ display: 'flex', gap: '6px', justifyContent: 'flex-end' }}>
                                        {!d.isValidated && <button className="btn btn-secondary btn-sm" onClick={() => setEditing(d)}>Revisar</button>}
                                        {!d.isValidated && <button className="btn btn-success btn-sm" onClick={() => approve(d.id)}>Aprobar</button>}
                                        {d.isValidated && <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>🔒 Contabilizado</span>}
                                    </div>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </PageContainer>
    );
}
