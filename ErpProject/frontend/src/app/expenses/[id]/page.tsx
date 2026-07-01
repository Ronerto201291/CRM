"use client";

import React, { useEffect, useState, useCallback } from "react";
import { useParams } from "next/navigation";
import PageContainer from "@/components/PageContainer";

interface ExpenseLine {
    id: string;
    description: string;
    quantity: number;
    unitPrice: number;
    taxRate: number;
    total: number;
}

interface ExpenseUpload {
    id: string;
    fileName: string;
    status: string;
    ocrConfidence?: number;
}

interface ExpenseDocumentDetail {
    id: string;
    invoiceNumber?: string;
    supplierName?: string;
    supplierTaxId?: string;
    issueDate?: string;
    taxBase?: number;
    vatRate?: number;
    vatAmount?: number;
    irpfRate?: number;
    irpfAmount?: number;
    total?: number;
    status: string;
    isValidated: boolean;
    validatedAt?: string;
    ocrData?: any;
    lines: ExpenseLine[];
    uploads: ExpenseUpload[];
}

const STATUS_MAP: Record<string, { label: string; cls: string }> = {
    Draft: { label: 'Borrador', cls: 'badge-gray' },
    Reviewed: { label: 'Revisado', cls: 'badge-info' },
    Approved: { label: 'Aprobado', cls: 'badge-success' },
    Rejected: { label: 'Rechazado', cls: 'badge-danger' },
};

export default function ExpenseDetailPage() {
    const { id } = useParams<{ id: string }>();
    const [doc, setDoc] = useState<ExpenseDocumentDetail | null>(null);
    const [loading, setLoading] = useState(true);
    const [actionLoading, setActionLoading] = useState<string | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const res = await fetch(`/api/proxy/expenses/${id}`);
            if (res.ok) setDoc(await res.json());
            else setDoc(null);
        } finally {
            setLoading(false);
        }
    }, [id]);

    useEffect(() => { load(); }, [load]);

    const doAction = async (action: string) => {
        setActionLoading(action);
        try {
            const res = await fetch(`/api/proxy/expenses/${id}/${action}`, { method: 'POST' });
            if (res.ok) { alert('Acción realizada'); load(); }
            else { const e = await res.json(); alert(e.error || 'Error'); }
        } finally {
            setActionLoading(null);
        }
    };

    const fmt = (n: number) => `€ ${n.toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;

    if (loading) return <PageContainer><div style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>Cargando...</div></PageContainer>;
    if (!doc) return <PageContainer><div style={{ padding: '40px', textAlign: 'center' }}>Documento no encontrado</div></PageContainer>;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Gasto {doc.invoiceNumber || doc.id.slice(0, 8)}</h1>
                    <p className="page-subtitle">{doc.supplierName || 'Sin proveedor'}</p>
                </div>
                <a href="/expenses" className="btn btn-secondary">← Volver</a>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '20px', marginBottom: '20px' }}>
                <div className="erp-card">
                    <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '12px' }}>Información del Proveedor</div>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
                        <div><span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>Nombre</span><div style={{ fontWeight: 600 }}>{doc.supplierName || '—'}</div></div>
                        {doc.supplierTaxId && <div><span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>NIF/CIF</span><div style={{ fontWeight: 600 }}>{doc.supplierTaxId}</div></div>}
                        {doc.issueDate && <div><span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>Fecha</span><div style={{ fontWeight: 600 }}>{new Date(doc.issueDate).toLocaleDateString('es-ES')}</div></div>}
                        <div><span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>Nº Factura</span><div style={{ fontWeight: 600, fontFamily: 'monospace' }}>{doc.invoiceNumber || '—'}</div></div>
                    </div>
                </div>
                <div className="erp-card" style={{ textAlign: 'center' }}>
                    <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '8px' }}>Estado</div>
                    <span className={`badge ${STATUS_MAP[doc.status]?.cls ?? 'badge-gray'}`} style={{ fontSize: '14px', padding: '8px 16px' }}>
                        {STATUS_MAP[doc.status]?.label ?? doc.status}
                    </span>
                    {doc.total !== undefined && <div style={{ marginTop: '16px', fontSize: '24px', fontWeight: 800, color: 'var(--brand-primary)' }}>{fmt(doc.total)}</div>}
                    {doc.isValidated && (
                        <div style={{ marginTop: '8px', fontSize: '12px', color: 'var(--success)' }}>✓ Validado OCR</div>
                    )}
                </div>
            </div>

            {doc.ocrData && doc.ocrConfidence !== undefined && (
                <div className="erp-card" style={{ marginBottom: '20px', borderLeft: '4px solid var(--brand-primary)' }}>
                    <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '8px' }}>Validación OCR</div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
                        <div style={{ fontSize: '20px', fontWeight: 800, color: doc.ocrConfidence > 0.85 ? 'var(--success)' : doc.ocrConfidence > 0.7 ? 'var(--warning)' : 'var(--danger)' }}>
                            {Math.round(doc.ocrConfidence * 100)}%
                        </div>
                        <div style={{ flex: 1, height: '8px', background: 'var(--border)', borderRadius: '4px', overflow: 'hidden' }}>
                            <div style={{ width: `${doc.ocrConfidence * 100}%`, height: '100%', background: doc.ocrConfidence > 0.85 ? 'var(--success)' : doc.ocrConfidence > 0.7 ? 'var(--warning)' : 'var(--danger)', borderRadius: '4px' }} />
                        </div>
                        <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Confianza OCR</span>
                    </div>
                    {doc.ocrData.ocrText && (
                        <div style={{ marginTop: '12px', padding: '10px', background: 'var(--surface-2)', borderRadius: '6px', fontSize: '12px', color: 'var(--text-secondary)', fontFamily: 'monospace' }}>
                            {doc.ocrData.ocrText}
                        </div>
                    )}
                </div>
            )}

            {doc.uploads && doc.uploads.length > 0 && (
                <div className="erp-card" style={{ marginBottom: '20px' }}>
                    <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '12px' }}>Documentos Adjuntos</div>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                        {doc.uploads.map(u => (
                            <div key={u.id} style={{ display: 'flex', alignItems: 'center', gap: '12px', padding: '10px 12px', background: 'var(--surface-2)', borderRadius: '6px' }}>
                                <span style={{ fontSize: '20px' }}>📄</span>
                                <div style={{ flex: 1 }}>
                                    <div style={{ fontWeight: 600, fontSize: '13px' }}>{u.fileName}</div>
                                    <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                                        {u.status === 'Processed' ? '✓ Procesado' : u.status === 'Pending' ? '⏳ Pendiente' : '❌ Error'}
                                        {u.ocrConfidence && ` · OCR: ${Math.round(u.ocrConfidence * 100)}%`}
                                    </div>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            )}

            <div className="erp-card" style={{ marginBottom: '20px' }}>
                <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '12px' }}>Líneas del Gasto</div>
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Descripción</th>
                            <th style={{ textAlign: 'right' }}>Cantidad</th>
                            <th style={{ textAlign: 'right' }}>P. Unit.</th>
                            <th style={{ textAlign: 'right' }}>IVA</th>
                            <th style={{ textAlign: 'right' }}>Total</th>
                        </tr>
                    </thead>
                    <tbody>
                        {doc.lines.map(line => (
                            <tr key={line.id}>
                                <td style={{ fontWeight: 500 }}>{line.description}</td>
                                <td style={{ textAlign: 'right' }}>{line.quantity}</td>
                                <td style={{ textAlign: 'right' }}>{fmt(line.unitPrice)}</td>
                                <td style={{ textAlign: 'right' }}>{line.taxRate}%</td>
                                <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(line.total)}</td>
                            </tr>
                        ))}
                    </tbody>
                    {doc.taxBase !== undefined && (
                        <tfoot style={{ background: 'var(--surface-2)' }}>
                            <tr>
                                <td colSpan={4} style={{ textAlign: 'right', padding: '8px 12px', fontWeight: 600 }}>Base imponible</td>
                                <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(doc.taxBase)}</td>
                            </tr>
                            {doc.vatAmount !== undefined && (
                                <tr>
                                    <td colSpan={4} style={{ textAlign: 'right', padding: '8px 12px', fontWeight: 600 }}>IVA ({doc.vatRate}%)</td>
                                    <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(doc.vatAmount)}</td>
                                </tr>
                            )}
                            {doc.irpfAmount !== undefined && doc.irpfAmount > 0 && (
                                <tr>
                                    <td colSpan={4} style={{ textAlign: 'right', padding: '8px 12px', fontWeight: 600 }}>IRPF ({doc.irpfRate}%)</td>
                                    <td style={{ textAlign: 'right', fontWeight: 700, color: 'var(--danger)' }}>-{fmt(doc.irpfAmount)}</td>
                                </tr>
                            )}
                            {doc.total !== undefined && (
                                <tr>
                                    <td colSpan={4} style={{ textAlign: 'right', padding: '8px 12px', fontWeight: 800, fontSize: '15px' }}>Total</td>
                                    <td style={{ textAlign: 'right', fontWeight: 800, fontSize: '15px', color: 'var(--brand-primary)' }}>{fmt(doc.total)}</td>
                                </tr>
                            )}
                        </tfoot>
                    )}
                </table>
            </div>

            <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                {doc.status === 'Draft' && (
                    <>
                        <button className="btn btn-secondary" onClick={() => doAction('approve')} disabled={!!actionLoading}>
                            {actionLoading === 'approve' ? 'Aprobando...' : '✓ Aprobar'}
                        </button>
                        <button className="btn btn-secondary" onClick={() => doAction('reject')} disabled={!!actionLoading} style={{ color: 'var(--danger)' }}>
                            {actionLoading === 'reject' ? 'Rechazando...' : '✕ Rechazar'}
                        </button>
                    </>
                )}
                {doc.status === 'Reviewed' && (
                    <button className="btn btn-primary" onClick={() => doAction('approve')} disabled={!!actionLoading}>
                        {actionLoading === 'approve' ? 'Aprobando...' : '✓ Aprobar'}
                    </button>
                )}
            </div>
        </PageContainer>
    );
}