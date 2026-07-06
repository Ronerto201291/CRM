'use client';
import { useState, useCallback } from 'react';
import PageContainer from '@/components/PageContainer';
import AccessibleModal from '@/components/AccessibleModal';

interface SerialNumber {
    id: string;
    serial: string;
    productId: string;
    lotId?: string;
    status: string;
    soldDate?: string;
}

interface Product {
    id: string;
    name: string;
    sku?: string;
}

const SERIAL_STATUSES = ['Available', 'Reserved', 'Sold', 'Defective'];

const emptyForm = () => ({ productId: '', serial: '' });

export default function SerialsClient({
    initialSerials,
    initialProducts,
}: {
    initialSerials: SerialNumber[];
    initialProducts: Product[];
}) {
    const [serials, setSerials] = useState(initialSerials);
    const [products] = useState(initialProducts);
    const [showModal, setShowModal] = useState(false);
    const [form, setForm] = useState(emptyForm());
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const productName = (id: string) => {
        const p = products.find(x => x.id === id);
        return p ? `${p.name}${p.sku ? ` (${p.sku})` : ''}` : id.slice(0, 8);
    };

    const load = useCallback(async () => {
        const res = await fetch('/api/proxy/v1/inventory/serials');
        if (res.ok) {
            const data = await res.json();
            setSerials(Array.isArray(data) ? data : []);
        }
    }, []);

    const handleCreate = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!form.productId || !form.serial.trim()) {
            setError('Producto y número de serie son obligatorios');
            return;
        }
        setSaving(true);
        setError(null);
        try {
            const res = await fetch('/api/proxy/v1/inventory/serials', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ productId: form.productId, serial: form.serial.trim() }),
            });
            if (!res.ok) {
                const err = await res.json().catch(() => ({}));
                throw new Error(err.error || err.title || 'Error al crear serie');
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

    const updateStatus = async (id: string, status: string) => {
        await fetch(`/api/proxy/v1/inventory/serials/${id}/status`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ status }),
        });
        await load();
    };

    const handleDelete = async (id: string) => {
        if (!confirm('¿Eliminar este número de serie?')) return;
        const res = await fetch(`/api/proxy/v1/inventory/serials/${id}`, { method: 'DELETE' });
        if (res.ok) await load();
    };

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Números de serie</h1>
                    <p className="page-subtitle">Trazabilidad unitaria de productos</p>
                </div>
                <button type="button" className="btn btn-primary" onClick={() => { setError(null); setShowModal(true); }}>
                    + Nuevo número de serie
                </button>
            </div>

            <div className="erp-card" style={{ overflowX: 'auto' }}>
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Serie</th>
                            <th>Producto</th>
                            <th>Estado</th>
                            <th>Vendido</th>
                            <th></th>
                        </tr>
                    </thead>
                    <tbody>
                        {serials.length === 0 ? (
                            <tr><td colSpan={5} style={{ textAlign: 'center', color: 'var(--text-muted)' }}>Sin números de serie</td></tr>
                        ) : serials.map(s => (
                            <tr key={s.id}>
                                <td style={{ fontWeight: 600, fontFamily: 'monospace' }}>{s.serial}</td>
                                <td>{productName(s.productId)}</td>
                                <td>
                                    <select className="erp-input" style={{ fontSize: '12px', padding: '4px 8px' }} value={s.status} onChange={ev => updateStatus(s.id, ev.target.value)}>
                                        {SERIAL_STATUSES.map(st => <option key={st} value={st}>{st}</option>)}
                                    </select>
                                </td>
                                <td>{s.soldDate ? new Date(s.soldDate).toLocaleDateString('es-ES') : '—'}</td>
                                <td>
                                    <button type="button" className="btn btn-secondary" style={{ fontSize: '12px' }} onClick={() => handleDelete(s.id)}>Eliminar</button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            <AccessibleModal open={showModal} onClose={() => setShowModal(false)} title="Nuevo número de serie">
                <form onSubmit={handleCreate}>
                    {error && <div style={{ color: 'var(--danger)', marginBottom: 12, fontSize: 13 }}>{error}</div>}
                    <label className="erp-label">Producto</label>
                    <select className="erp-input" value={form.productId} onChange={ev => setForm({ ...form, productId: ev.target.value })} required>
                        <option value="">Seleccionar...</option>
                        {products.map(p => <option key={p.id} value={p.id}>{p.name}{p.sku ? ` (${p.sku})` : ''}</option>)}
                    </select>
                    <label className="erp-label" style={{ marginTop: 12 }}>Número de serie</label>
                    <input className="erp-input" value={form.serial} onChange={ev => setForm({ ...form, serial: ev.target.value })} required />
                    <div style={{ display: 'flex', gap: 8, marginTop: 20, justifyContent: 'flex-end' }}>
                        <button type="button" className="btn btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
                        <button type="submit" className="btn btn-primary" disabled={saving}>{saving ? 'Guardando...' : 'Crear'}</button>
                    </div>
                </form>
            </AccessibleModal>
        </PageContainer>
    );
}
