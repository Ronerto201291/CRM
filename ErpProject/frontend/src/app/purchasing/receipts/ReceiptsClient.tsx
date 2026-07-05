'use client';
import { useState } from 'react';
import PageContainer from '@/components/PageContainer';

interface GoodsReceipt {
    id: string;
    number: string;
    receiptDate: string;
    purchaseOrderId?: string;
    supplierName?: string;
    status: 'Draft' | 'Completed';
    lineCount: number;
    createdAt: string;
}

const STATUS_MAP: Record<string, { label: string; cls: string }> = {
    Draft: { label: 'Borrador', cls: 'badge-gray' },
    Completed: { label: 'Completado', cls: 'badge-success' },
};

export default function ReceiptsClient({ initialReceipts }: { initialReceipts: GoodsReceipt[] }) {
    const receipts = initialReceipts;
    const [filter, setFilter] = useState('all');

    const filtered = filter === 'all' ? receipts : receipts.filter(r => r.status === filter);

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Recepciones de Compra</h1>
                    <p className="page-subtitle">Albaranes y recepciones de mercancía</p>
                </div>
                <a href="/purchasing/receipts/new" className="btn btn-primary">
                    + Nueva Recepción
                </a>
            </div>

            <div style={{ display: 'flex', gap: '6px', marginBottom: '20px' }}>
                {['all', 'Draft', 'Completed'].map(f => (
                    <button key={f} onClick={() => setFilter(f)}
                        style={{
                            padding: '6px 14px', borderRadius: '6px', border: '1px solid var(--border)',
                            fontSize: '12px', fontWeight: filter === f ? 700 : 500,
                            background: filter === f ? 'var(--brand-primary)' : 'var(--surface)',
                            color: filter === f ? 'white' : 'var(--text-secondary)',
                            cursor: 'pointer',
                        }}>
                        {f === 'all' ? 'Todos' : STATUS_MAP[f]?.label ?? f}
                        {f !== 'all' && (
                            <span style={{ marginLeft: '6px', opacity: 0.7 }}>
                                ({receipts.filter(r => r.status === f).length})
                            </span>
                        )}
                    </button>
                ))}
            </div>

            <div className="erp-card" style={{ overflow: 'hidden' }}>
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Número</th>
                            <th>Proveedor</th>
                            <th>Fecha</th>
                            <th style={{ textAlign: 'right' }}>Líneas</th>
                            <th>Estado</th>
                            <th>Fecha creación</th>
                        </tr>
                    </thead>
                    <tbody>
                        {filtered.length === 0 && (
                            <tr><td colSpan={6}>
                                <div className="empty-state">
                                    <div className="empty-state-icon">📦</div>
                                    <div className="empty-state-title">Sin recepciones</div>
                                    <div className="empty-state-sub">Las recepciones se crean desde pedidos de compra</div>
                                </div>
                            </td></tr>
                        )}
                        {filtered.map(r => (
                            <tr key={r.id}>
                                <td style={{ fontWeight: 700, fontFamily: 'monospace', fontSize: '12px' }}>{r.number}</td>
                                <td style={{ color: 'var(--text-secondary)' }}>{r.supplierName || '—'}</td>
                                <td style={{ color: 'var(--text-secondary)' }}>{new Date(r.receiptDate).toLocaleDateString('es-ES')}</td>
                                <td style={{ textAlign: 'right' }}>{r.lineCount}</td>
                                <td><span className={`badge ${STATUS_MAP[r.status]?.cls ?? 'badge-gray'}`}>{STATUS_MAP[r.status]?.label ?? r.status}</span></td>
                                <td style={{ color: 'var(--text-secondary)' }}>{new Date(r.createdAt).toLocaleDateString('es-ES')}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </PageContainer>
    );
}
