'use client';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import PageContainer from '@/components/PageContainer';
import AccessibleModal from '@/components/AccessibleModal';
import FormLabel from '@/components/FormLabel';
import { warehouseFormSchema, type WarehouseFormValues } from '@/lib/schemas/warehouseSchema';

export interface Warehouse {
    id: string;
    name: string;
    location?: string;
    isActive: boolean;
}

interface Stock {
    productId: string;
    productName: string;
    sku: string;
    quantity: number;
}

interface WarehousesClientProps {
    initialWarehouses: Warehouse[];
}

export default function WarehousesClient({ initialWarehouses }: WarehousesClientProps) {
    const [warehouses, setWarehouses] = useState<Warehouse[]>(initialWarehouses);
    const [selected, setSelected] = useState<Warehouse | null>(null);
    const [stocks, setStocks] = useState<Stock[]>([]);
    const [showStock, setShowStock] = useState(false);
    const [loadingStock, setLoadingStock] = useState(false);
    const [showModal, setShowModal] = useState(false);
    const [editing, setEditing] = useState<Warehouse | null>(null);
    const { register, handleSubmit, reset, formState: { errors } } = useForm<WarehouseFormValues>({
        resolver: zodResolver(warehouseFormSchema),
        defaultValues: { name: '', location: '' },
    });

    const load = async () => {
        const r = await fetch('/api/proxy/inventory/warehouses');
        if (r.ok) setWarehouses(await r.json());
    };

    const openStock = async (wh: Warehouse) => {
        setSelected(wh);
        setStocks([]);
        setShowStock(true);
        setLoadingStock(true);
        const r = await fetch(`/api/proxy/inventory/stock?warehouseId=${wh.id}`);
        if (r.ok) setStocks(await r.json());
        setLoadingStock(false);
    };

    const onSave = handleSubmit(async (data) => {
        const method = editing ? 'PUT' : 'POST';
        const url = editing ? `/api/proxy/inventory/warehouses/${editing.id}` : '/api/proxy/inventory/warehouses';
        await fetch(url, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
        setShowModal(false);
        setEditing(null);
        reset({ name: '', location: '' });
        load();
    });

    const openEdit = (wh: Warehouse) => {
        setEditing(wh);
        reset({ name: wh.name, location: wh.location || '' });
        setShowModal(true);
    };

    const openNew = () => {
        setEditing(null);
        reset({ name: '', location: '' });
        setShowModal(true);
    };

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Almacenes</h1>
                    <p className="page-subtitle">Gestión de ubicaciones de stock</p>
                </div>
                <button className="btn btn-primary" onClick={openNew}>
                    <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}><path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" /></svg>
                    Nuevo Almacén
                </button>
            </div>

            {warehouses.length === 0 ? (
                <div className="erp-card" style={{ padding: '56px', textAlign: 'center' }}>
                    <div className="empty-state">
                        <div className="empty-state-icon">🏭</div>
                        <div className="empty-state-title">Sin almacenes</div>
                        <div className="empty-state-sub">Crea tu primer almacén</div>
                    </div>
                </div>
            ) : (
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))', gap: '16px' }}>
                    {warehouses.map(wh => (
                        <div key={wh.id} className="erp-card" style={{ padding: '20px' }}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '12px' }}>
                                <div>
                                    <div style={{ fontWeight: 700, fontSize: '15px', color: 'var(--text-primary)' }}>{wh.name}</div>
                                    {wh.location && <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '3px' }}>📍 {wh.location}</div>}
                                </div>
                                <span className={`badge ${wh.isActive ? 'badge-success' : 'badge-gray'}`}>{wh.isActive ? 'Activo' : 'Inactivo'}</span>
                            </div>
                            <div style={{ display: 'flex', gap: '8px' }}>
                                <button className="btn btn-secondary btn-sm" style={{ flex: 1 }} onClick={() => openStock(wh)}>
                                    Ver stock
                                </button>
                                <button className="btn btn-secondary btn-sm" onClick={() => openEdit(wh)}>
                                    Editar
                                </button>
                            </div>
                        </div>
                    ))}
                </div>
            )}

            <AccessibleModal
                open={showStock && !!selected}
                onClose={() => setShowStock(false)}
                title={selected ? `Stock en: ${selected.name}` : 'Stock'}
                maxWidth="640px"
            >
                {selected?.location && (
                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '-12px', marginBottom: '16px' }}>📍 {selected.location}</div>
                )}
                <div style={{ overflowY: 'auto', maxHeight: '60vh' }}>
                    {loadingStock ? (
                        <div style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)', fontSize: '13px' }}>Cargando stock...</div>
                    ) : (
                        <table className="erp-table">
                            <thead>
                                <tr>
                                    <th>Producto</th>
                                    <th>SKU</th>
                                    <th style={{ textAlign: 'right' }}>Cantidad</th>
                                    <th>Estado</th>
                                </tr>
                            </thead>
                            <tbody>
                                {stocks.length === 0 && (
                                    <tr><td colSpan={4}>
                                        <div className="empty-state" style={{ padding: '32px' }}>
                                            <div className="empty-state-icon">📦</div>
                                            <div className="empty-state-title">Sin stock registrado</div>
                                        </div>
                                    </td></tr>
                                )}
                                {stocks.map((s, i) => (
                                    <tr key={i}>
                                        <td><span style={{ fontWeight: 600 }}>{s.productName}</span></td>
                                        <td><span style={{ fontFamily: 'monospace', fontSize: '12px', color: 'var(--text-secondary)' }}>{s.sku || '—'}</span></td>
                                        <td style={{ textAlign: 'right' }}>
                                            <span style={{ fontWeight: 800, fontSize: '16px', color: s.quantity < 5 ? 'var(--warning)' : 'var(--text-primary)' }}>{s.quantity}</span>
                                        </td>
                                        <td>
                                            {s.quantity === 0 && <span className="badge badge-danger">Sin stock</span>}
                                            {s.quantity > 0 && s.quantity < 5 && <span className="badge badge-warning">Bajo stock</span>}
                                            {s.quantity >= 5 && <span className="badge badge-success">OK</span>}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    )}
                </div>
            </AccessibleModal>

            <AccessibleModal
                open={showModal}
                onClose={() => setShowModal(false)}
                title={`${editing ? 'Editar' : 'Nuevo'} Almacén`}
                maxWidth="440px"
                footer={(
                    <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                        <button type="button" className="btn btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
                        <button type="submit" form="warehouse-form" className="btn btn-primary">✓ Guardar</button>
                    </div>
                )}
            >
                <form id="warehouse-form" onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                    <div className="form-group">
                        <FormLabel htmlFor="wh-name" required>Nombre</FormLabel>
                        <input id="wh-name" className="erp-input" {...register('name')} placeholder="Almacén Principal" />
                        {errors.name && <p style={{ color: 'var(--danger)', fontSize: 12 }}>{errors.name.message}</p>}
                    </div>
                    <div className="form-group">
                        <FormLabel htmlFor="wh-location">Ubicación</FormLabel>
                        <input id="wh-location" className="erp-input" {...register('location')} placeholder="Calle Ejemplo 1, Madrid" />
                    </div>
                </form>
            </AccessibleModal>
        </PageContainer>
    );
}
