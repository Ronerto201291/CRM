'use client';
import { useState, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import { parseListResponse } from '@/lib/parseListResponse';
import PageContainer from '@/components/PageContainer';
import AccessibleModal from '@/components/AccessibleModal';
import { creditNoteCreateSchema } from '@/lib/schemas/legacyFormSchemas';

interface Invoice {
    id: string;
    number: string;
    clientName?: string;
    issueDate: string;
    total: number;
    status: string;
    isLocked: boolean;
}

interface CreditNoteRequest {
    invoiceId: string;
    reason: string;
    referenceDateToCorrectionMessage?: string;
}

interface CreditNotesClientProps {
    initialInvoices: Invoice[];
    initialCreditNotes: Invoice[];
}

export default function CreditNotesClient({
    initialInvoices,
    initialCreditNotes,
}: CreditNotesClientProps) {
    const router = useRouter();
    const [invoices, setInvoices] = useState<Invoice[]>(initialInvoices);
    const [creditNotes, setCreditNotes] = useState<Invoice[]>(initialCreditNotes);
    const [loading, setLoading] = useState(false);
    const [showModal, setShowModal] = useState(false);
    const [selectedInvoice, setSelectedInvoice] = useState<Invoice | null>(null);
    const [form, setForm] = useState<CreditNoteRequest>({
        invoiceId: '',
        reason: '',
        referenceDateToCorrectionMessage: new Date().toISOString().split('T')[0],
    });
    const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const [invRes, creditRes] = await Promise.all([
                fetch('/api/proxy/invoices?pageSize=500'),
                fetch('/api/proxy/invoices?type=CreditNote&pageSize=500'),
            ]);
            if (invRes.ok) {
                const allInvoices = parseListResponse<Invoice>(await invRes.json());
                setInvoices(allInvoices.filter((i) => i.isLocked || i.status === 'Locked'));
            }
            if (creditRes.ok) {
                setCreditNotes(parseListResponse<Invoice>(await creditRes.json()));
            }
        } catch (err) {
            console.error('Error cargando facturas:', err);
        } finally {
            setLoading(false);
        }
    }, []);

    const handleCreateCreditNote = async () => {
        const parsed = creditNoteCreateSchema.safeParse(form);
        if (!parsed.success) {
            setMessage({ type: 'error', text: parsed.error.issues[0]?.message ?? 'Revisa el formulario' });
            return;
        }

        try {
            const response = await fetch(`/api/proxy/invoices/${parsed.data.invoiceId}/credit-note`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(form),
            });

            if (response.ok) {
                const result = await response.json();
                setMessage({ type: 'success', text: result.message });
                setShowModal(false);
                setForm({ invoiceId: '', reason: '', referenceDateToCorrectionMessage: new Date().toISOString().split('T')[0] });
                setTimeout(() => load(), 1000);
            } else {
                setMessage({ type: 'error', text: 'Error creando la factura rectificativa' });
            }
        } catch (err) {
            setMessage({ type: 'error', text: 'Error en la solicitud' });
            console.error(err);
        }
    };

    const fmt = (n: number) => `€ ${(n || 0).toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

    return (
        <PageContainer>
            {/* Header */}
            <div className="page-header">
                <div>
                    <h1 className="page-title">Abonos y Rectificativas</h1>
                    <p className="page-subtitle">Gestión de facturas rectificativas conforme RD 1619/2012</p>
                </div>
            </div>

            {/* Mensaje */}
            {message && (
                <div style={{
                    marginBottom: '16px', padding: '12px 16px', borderRadius: '6px', fontSize: '13px',
                    background: message.type === 'success' ? 'var(--success-bg)' : 'var(--danger-bg)',
                    border: `1px solid ${message.type === 'success' ? 'rgba(16,185,129,0.2)' : 'rgba(239,68,68,0.2)'}`,
                    color: message.type === 'success' ? 'var(--success)' : 'var(--danger)',
                }}>
                    {message.type === 'success' ? '✓' : '⚠'} {message.text}
                </div>
            )}

            {/* Tabs */}
            <div style={{ display: 'flex', gap: '16px', marginBottom: '24px', borderBottom: '1px solid var(--border)', paddingBottom: '12px' }}>
                <div style={{
                    paddingBottom: '8px', borderBottom: '3px solid #2563eb',
                    color: 'var(--text-primary)', fontWeight: 700, fontSize: '14px',
                }}>
                    ?? Facturas a Rectificar
                </div>
                <div style={{
                    paddingBottom: '8px',
                    color: 'var(--text-muted)', fontWeight: 700, fontSize: '14px', cursor: 'pointer',
                }} onClick={() => router.push('/billing')}>
                    ?? Todas las Facturas
                </div>
            </div>

            {/* Botón Crear */}
            <button className="btn btn-primary" style={{ marginBottom: '20px' }} onClick={() => setShowModal(true)}>
                + Crear Factura Rectificativa
            </button>

            {/* Tabla de Facturas a Rectificar */}
            {loading ? (
                <div style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
                    <p>? Cargando facturas...</p>
                </div>
            ) : invoices.length === 0 ? (
                <div className="erp-card" style={{ padding: '40px', textAlign: 'center' }}>
                    <p style={{ fontSize: '14px', color: 'var(--text-muted)', marginBottom: '12px' }}>
                        ?? No hay facturas bloqueadas disponibles para rectificar
                    </p>
                    <p style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                        Solo las facturas con estado &quot;Bloqueada&quot; pueden ser rectificadas
                    </p>
                </div>
            ) : (
                <div className="erp-card" style={{ overflow: 'auto' }}>
                    <table className="erp-table">
                        <thead>
                            <tr>
                                <th>Número</th>
                                <th>Cliente</th>
                                <th>Fecha</th>
                                <th style={{ textAlign: 'right' }}>Importe</th>
                                <th style={{ textAlign: 'center' }}>Acciones</th>
                            </tr>
                        </thead>
                        <tbody>
                            {invoices.map((inv) => (
                                <tr key={inv.id}>
                                    <td style={{ fontWeight: 600 }}>#{inv.number}</td>
                                    <td>{inv.clientName || 'Sin cliente'}</td>
                                    <td>{new Date(inv.issueDate).toLocaleDateString('es-ES')}</td>
                                    <td style={{ textAlign: 'right', fontWeight: 600 }}>{fmt(inv.total)}</td>
                                    <td style={{ textAlign: 'center' }}>
                                        <button
                                            className="btn btn-danger btn-sm"
                                            onClick={() => {
                                                setSelectedInvoice(inv);
                                                setForm({ ...form, invoiceId: inv.id });
                                                setShowModal(true);
                                            }}
                                        >
                                            Rectificar
                                        </button>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            {/* Sección de Abonos Creados */}
            {creditNotes.length > 0 && (
                <div style={{ marginTop: '32px' }}>
                    <h2 style={{ fontSize: '16px', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '16px' }}>
                        ? Abonos Creados
                    </h2>
                    <div className="erp-card" style={{ overflow: 'auto' }}>
                        <table className="erp-table">
                            <thead>
                                <tr>
                                    <th>Número Abono</th>
                                    <th>Fecha</th>
                                    <th style={{ textAlign: 'right' }}>Importe (Negativo)</th>
                                    <th>Estado</th>
                                </tr>
                            </thead>
                            <tbody>
                                {creditNotes.map((cn) => (
                                    <tr key={cn.id}>
                                        <td style={{ fontWeight: 600 }}>#{cn.number}</td>
                                        <td>{new Date(cn.issueDate).toLocaleDateString('es-ES')}</td>
                                        <td style={{ textAlign: 'right', color: 'var(--danger)', fontWeight: 600 }}>-{fmt(cn.total)}</td>
                                        <td>
                                            <span className={`badge ${cn.status === 'Paid' ? 'badge-success' : 'badge-info'}`}>
                                                {cn.status === 'Paid' ? 'Contabilizada' : cn.status}
                                            </span>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </div>
            )}

            {/* Modal */}
            <AccessibleModal
                open={showModal}
                onClose={() => { setShowModal(false); setSelectedInvoice(null); }}
                title="Crear Factura Rectificativa"
                maxWidth="500px"
                footer={(
                    <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                        <button className="btn btn-secondary" onClick={() => {
                            setShowModal(false);
                            setSelectedInvoice(null);
                            setForm({ invoiceId: '', reason: '', referenceDateToCorrectionMessage: new Date().toISOString().split('T')[0] });
                        }}>
                            Cancelar
                        </button>
                        <button className="btn btn-danger" onClick={handleCreateCreditNote}>
                            Crear Rectificativa
                        </button>
                    </div>
                )}
            >

                        {selectedInvoice && (
                            <div style={{ padding: '12px', background: 'var(--surface-2)', borderRadius: '6px', marginBottom: '16px', fontSize: '12px' }}>
                                <div style={{ color: 'var(--text-muted)', marginBottom: '4px' }}>Factura original:</div>
                                <div style={{ fontWeight: 600, color: 'var(--text-primary)', marginBottom: '4px' }}>
                                    #{selectedInvoice.number} - {selectedInvoice.clientName}
                                </div>
                                <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                                    Importe: {fmt(selectedInvoice.total)}
                                </div>
                            </div>
                        )}

                        <div className="form-group" style={{ marginBottom: '16px' }}>
                            <label className="erp-label">MOTIVO DE RECTIFICACIÓN *</label>
                            <textarea
                                className="erp-input"
                                value={form.reason}
                                onChange={(e) => setForm({ ...form, reason: e.target.value })}
                                placeholder="Ej: Error en descripción de línea, cliente desacuerdo..."
                                style={{ minHeight: '80px', resize: 'vertical' }}
                            />
                        </div>
            </AccessibleModal>
        </PageContainer>
    );
}
