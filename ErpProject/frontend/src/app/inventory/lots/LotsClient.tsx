'use client';
import { useState, useCallback } from 'react';
import PageContainer from '@/components/PageContainer';
import AccessibleModal from '@/components/AccessibleModal';

interface ProductLot {
    id: string;
    productId: string;
    lotNumber: string;
    expirationDate?: string;
    quantity: number;
    unitCost: number;
}

interface Product {
    id: string;
    name: string;
    sku?: string;
}

const emptyForm = () => ({
    productId: '',
    lotNumber: '',
    expirationDate: new Date(Date.now() + 365 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10),
    quantity: '',
    unitCost: '',
});

export default function LotsClient({
    initialLots,
    initialProducts,
}: {
    initialLots: ProductLot[];
    initialProducts: Product[];
}) {
    const [lots, setLots] = useState(initialLots);
    const [products] = useState(initialProducts);
    const [showModal, setShowModal] = useState(false);
    const [form, setForm] = useState(emptyForm());
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [deletingId, setDeletingId] = useState<string | null>(null);

    const productName = (id: string) => {
        const p = products.find(x => x.id === id);
        return p ? `${p.name}${p.sku ? ` (${p.sku})` : ''}` : id.slice(0, 8);
    };

    const load = useCallback(async () => {
        const res = await fetch('/api/proxy/v1/inventory/lots');
        if (res.ok) {
            const data = await res.json();
            setLots(Array.isArray(data) ? data : []);
        }
    }, []);

    const handleCreate = async (e: React.FormEvent) => {
        e.preventDefault();
        const qty = parseFloat(form.quantity);
        const cost = parseFloat(form.unitCost);
        if (!form.productId || !form.lotNumber.trim() || Number.isNaN(qty) || qty <= 0) {
            setError('Producto, número de lote y cantidad son obligatorios');
            return;
        }
        setSaving(true);
        setError(null);
        try {
            const res = await fetch('/api/proxy/v1/inventory/lots', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    productId: form.productId,
                    lotNumber: form.lotNumber.trim(),
                    expirationDate: form.expirationDate,
                    quantity: qty,
                    unitCost: Number.isNaN(cost) ? 0 : cost,
                }),
            });
            if (!res.ok) {
                const err = await res.json().catch(() => ({}));
                throw new Error(err.error || err.title || 'Error al crear lote');
            }
            setShowModal(false);
            setForm(emptyForm());
            await load();
        } catch (err: unknown) {
            setError(err instanceof Error ? err.message : 'Error');
        } finally {
            setSaving(false);
        }
    };

    const handleDelete = async (id: string) => {
        if (!confirm('¿Eliminar este lote?')) return;
        setDeletingId(id);
        try {
            const res = await fetch(`/api/proxy/v1/inventory/lots/${id}`, { method: 'DELETE' });
            if (res.ok) await load();
        } finally {
            setDeletingId(null);
        }
    };

    const fmt = (n: number) => `€ ${(n || 0).toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Lotes de producto</h1>
                    <p className="page-subtitle">Trazabilidad por lote y caducidad</p>
                </div>
                <button type="button" className="btn btn-primary" onClick={() => { setError(null); setShowModal(true); }}>
                    + Nuevo lote
                </button>
            </div>

            <div className="erp-card" style={{ overflowX: 'auto' }}>
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Lote</th>
                            <th>Producto</th>
                            <th>Caducidad</th>
                            <th style={{ textAlign: 'right' }}>Cantidad</th>
                            <th style={{ textAlign: 'right' }}>Coste unit.</th>
                            <th></th>
                        </tr>
                    </thead>
                    <tbody>
                        {lots.length === 0 ? (
                            <tr><td colSpan={6} style={{ textAlign: 'center', color: 'var(--text-muted)' }}>Sin lotes</td></tr>
                        ) : lots.map(l => (
                            <tr key={l.id}>
                                <td style={{ fontWeight: 600 }}>{l.lotNumber}</td>
                                <td>{productName(l.productId)}</td>
                                <td>{l.expirationDate ? new Date(l.expirationDate).toLocaleDateString('es-ES') : '—'}</td>
                                <td style={{ textAlign: 'right' }}>{l.quantity}</td>
                                <td style={{ textAlign: 'right' }}>{fmt(l.unitCost)}</td>
                                <td>
                                    <button type="button" className="btn btn-secondary" style={{ fontSize: '12px' }} disabled={deletingId === l.id} onClick={() => handleDelete(l.id)}>
                                        {deletingId === l.id ? '...' : 'Eliminar'}
                                    </button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            <AccessibleModal open={showModal} onClose={() => setShowModal(false)} title="Nuevo lote">
                <form onSubmit={handleCreate}>
                    {error && <div style={{ color: 'var(--danger)', marginBottom: 12, fontSize: 13 }}>{error}</div>}
                    <label className="erp-label">Producto</label>
                    <select className="erp-input" value={form.productId} onChange={ev => setForm({ ...form, productId: ev.target.value })} required>
                        <option value="">Seleccionar...</option>
                        {products.map(p => <option key={p.id} value={p.id}>{p.name}{p.sku ? ` (${p.sku})` : ''}</option>)}
                    </select>
                    <label className="erp-label" style={{ marginTop: 12 }}>Número de lote</label>
                    <input className="erp-input" value={form.lotNumber} onChange={ev => setForm({ ...form, lotNumber: ev.target.value })} required />
                    <label className="erp-label" style={{ marginTop: 12 }}>Fecha caducidad</label>
                    <input className="erp-input" type="date" value={form.expirationDate} onChange={ev => setForm({ ...form, expirationDate: ev.target.value })} required />
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginTop: 12 }}>
                        <div>
                            <label className="erp-label">Cantidad</label>
                            <input className="erp-input" type="number" step="0.01" min="0.01" value={form.quantity} onChange={ev => setForm({ ...form, quantity: ev.target.value })} required />
                        </div>
                        <div>
                            <label className="erp-label">Coste unitario (€)</label>
                            <input className="erp-input" type="number" step="0.01" min="0" value={form.unitCost} onChange={ev => setForm({ ...form, unitCost: ev.target.value })} />
                        </div>
                    </div>
                    <div style={{ display: 'flex', gap: 8, marginTop: 20, justifyContent: 'flex-end' }}>
                        <button type="button" className="btn btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
                        <button type="submit" className="btn btn-primary" disabled={saving}>{saving ? 'Guardando...' : 'Crear'}</button>
                    </div>
                </form>
            </AccessibleModal>
        </PageContainer>
    );
}
