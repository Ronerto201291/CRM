'use client';
import { useEffect, useState } from 'react';
import PageContainer from '@/components/PageContainer';
import AccessibleModal from '@/components/AccessibleModal';
import FormLabel from '@/components/FormLabel';
import { parseListResponse } from '@/lib/parseListResponse';
import { useCachedApi } from '@/hooks/useCachedApi';
import { productFormSchema } from '@/lib/schemas/productSchema';
import { stockAdjustmentSchema } from '@/lib/schemas/stockAdjustmentSchema';

interface Product {
    id: string;
    name: string;
    sku: string;
    costPrice: number;
    salePrice: number;
    vatPercent: number;
    totalStock: number;
    reorderPoint: number;
    isActive: boolean;
    type: string;
}

interface Warehouse {
    id: string;
    name: string;
}

interface Movement {
    id: string;
    productName: string;
    warehouseName: string;
    movementType: string;
    quantity: number;
    reason: string;
    createdAt: string;
}

type Tab = 'products' | 'movements';

export default function InventoryClient({
    initialProducts,
    initialWarehouses,
}: {
    initialProducts: Product[];
    initialWarehouses: Warehouse[];
}) {
    const [tab, setTab] = useState<Tab>('products');
    const [products, setProducts] = useState<Product[]>(initialProducts);
    const [warehouses, setWarehouses] = useState<Warehouse[]>(initialWarehouses);
    const [movements, setMovements] = useState<Movement[]>([]);
    const [movPage, setMovPage] = useState(1);
    const [movTotal, setMovTotal] = useState(0);

    const [showModal, setShowModal] = useState(false);
    const [showAdjModal, setShowAdjModal] = useState(false);
    const [adjProduct, setAdjProduct] = useState<Product | null>(null);
    const [adjForm, setAdjForm] = useState({ warehouseId: '', quantity: 0, movementType: 'StockIn', reason: '' });

    const [form, setForm] = useState({ name: '', sku: '', price: 0, taxRate: 21, reorderPoint: 5, reorderQty: 10 });
    const [formError, setFormError] = useState('');
    const [adjError, setAdjError] = useState('');
    const [search, setSearch] = useState('');
    const { fetchCached, invalidateCached } = useCachedApi();

    const load = async () => {
        const path = `inventory/products?search=${encodeURIComponent(search)}&pageSize=500`;
        const data = await fetchCached<unknown>(path);
        if (data) setProducts(parseListResponse<Product>(data));
    };

    const loadWarehouses = async () => {
        const data = await fetchCached<unknown>('inventory/warehouses');
        if (data) setWarehouses(parseListResponse<Warehouse>(data));
    };

    const loadMovements = async (page = 1) => {
        const r = await fetch(`/api/proxy/inventory/movements?page=${page}&pageSize=20`);
        if (r.ok) {
            const data = await r.json();
            setMovements(data.items ?? data);
            setMovTotal(data.total ?? 0);
            setMovPage(page);
        }
    };

    useEffect(() => { if (tab === 'movements') loadMovements(1); }, [tab]);

    const handleSave = async () => {
        setFormError('');
        const parsed = productFormSchema.safeParse(form);
        if (!parsed.success) {
            setFormError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');
            return;
        }
        const r = await fetch('/api/proxy/inventory/products', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(parsed.data)
        });
        if (r.ok) {
            setShowModal(false);
            setForm({ name: '', sku: '', price: 0, taxRate: 21, reorderPoint: 5, reorderQty: 10 });
            invalidateCached('inventory/products');
            load();
        }
    };

    const openAdjModal = (product: Product) => {
        setAdjProduct(product);
        setAdjForm({ warehouseId: warehouses[0]?.id ?? '', quantity: 0, movementType: 'StockIn', reason: '' });
        setShowAdjModal(true);
    };

    const handleAdjust = async () => {
        if (!adjProduct) return;
        const parsed = stockAdjustmentSchema.safeParse(adjForm);
        if (!parsed.success) {
            setAdjError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');
            return;
        }
        setAdjError('');
        const r = await fetch('/api/proxy/inventory/stock/adjustment', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                productId: adjProduct.id,
                warehouseId: parsed.data.warehouseId,
                quantity: parsed.data.movementType === 'StockOut' ? -Math.abs(parsed.data.quantity) : Math.abs(parsed.data.quantity),
                movementType: parsed.data.movementType,
                reason: parsed.data.reason
            })
        });
        if (r.ok) {
            setShowAdjModal(false);
            load();
        }
    };

    const toggleActive = async (product: Product) => {
        await fetch(`/api/proxy/inventory/products/${product.id}/activate`, { method: 'PATCH' });
        load();
    };

    const totalValue = products.reduce((s, p) => s + (p.totalStock ?? 0) * p.salePrice, 0);
    const lowStock = products.filter(p => (p.totalStock ?? 0) <= (p.reorderPoint ?? 0)).length;
    const filtered = products.filter(p =>
        !search || p.name.toLowerCase().includes(search.toLowerCase()) || p.sku?.toLowerCase().includes(search.toLowerCase())
    );

    const movPages = Math.ceil(movTotal / 20);

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Inventario</h1>
                    <p className="page-subtitle">Productos · Stock · Movimientos</p>
                </div>
                <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
                    {tab === 'products' && (
                        <>
                            <div style={{ position: 'relative' }}>
                                <input className="erp-input" placeholder="Buscar producto..." value={search}
                                    onChange={e => { setSearch(e.target.value); }}
                                    onKeyDown={e => e.key === 'Enter' && load()}
                                    style={{ paddingLeft: '32px', width: '200px' }} />
                                <span style={{ position: 'absolute', left: '10px', top: '50%', transform: 'translateY(-50%)', color: 'var(--text-muted)' }}>🔍</span>
                            </div>
                            <button className="btn btn-primary" onClick={() => setShowModal(true)}>
                                <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}><path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" /></svg>
                                Nuevo Producto
                            </button>
                        </>
                    )}
                </div>
            </div>

            {/* KPIs */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '14px', marginBottom: '20px' }}>
                <div className="erp-card" style={{ padding: '16px 20px' }}>
                    <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '4px' }}>Total Productos</div>
                    <div style={{ fontSize: '28px', fontWeight: 800, color: 'var(--brand-primary)' }}>{products.length}</div>
                </div>
                <div className="erp-card" style={{ padding: '16px 20px' }}>
                    <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '4px' }}>Valor en Stock</div>
                    <div style={{ fontSize: '22px', fontWeight: 800, color: 'var(--success)' }}>€ {totalValue.toFixed(2)}</div>
                </div>
                <div className="erp-card" style={{ padding: '16px 20px' }}>
                    <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '4px' }}>Bajo Punto de Reorden</div>
                    <div style={{ fontSize: '28px', fontWeight: 800, color: lowStock > 0 ? 'var(--warning)' : 'var(--text-primary)' }}>{lowStock}</div>
                </div>
            </div>

            {/* Tabs */}
            <div style={{ display: 'flex', gap: '4px', marginBottom: '16px', borderBottom: '1px solid var(--border)', paddingBottom: '0' }}>
                {(['products', 'movements'] as Tab[]).map(t => (
                    <button key={t} onClick={() => setTab(t)}
                        style={{ padding: '8px 18px', border: 'none', borderBottom: tab === t ? '2px solid var(--brand-primary)' : '2px solid transparent', background: 'none', cursor: 'pointer', fontWeight: tab === t ? 700 : 500, color: tab === t ? 'var(--brand-primary)' : 'var(--text-secondary)', fontSize: '13px' }}>
                        {t === 'products' ? 'Productos' : 'Movimientos'}
                    </button>
                ))}
            </div>

            {/* Products Tab */}
            {tab === 'products' && (
                <div className="erp-card" style={{ overflow: 'hidden' }}>
                    <table className="erp-table">
                        <thead><tr>
                            <th>Producto</th><th>SKU</th>
                            <th style={{ textAlign: 'right' }}>Coste</th>
                            <th style={{ textAlign: 'right' }}>Venta</th>
                            <th>IVA</th>
                            <th style={{ textAlign: 'right' }}>Stock</th>
                            <th style={{ textAlign: 'right' }}>Reorden</th>
                            <th style={{ textAlign: 'right' }}>Valor</th>
                            <th style={{ textAlign: 'right' }}>Acciones</th>
                        </tr></thead>
                        <tbody>
                            {filtered.length === 0 && (
                                <tr><td colSpan={9}><div className="empty-state"><div className="empty-state-icon">📦</div><div className="empty-state-title">Sin productos</div><div className="empty-state-sub">Añade productos al catálogo para usar en facturas</div></div></td></tr>
                            )}
                            {filtered.map(p => {
                                const stock = p.totalStock ?? 0;
                                const reorder = p.reorderPoint ?? 0;
                                const isLow = stock <= reorder;
                                return (
                                    <tr key={p.id} style={{ opacity: p.isActive ? 1 : 0.5 }}>
                                        <td>
                                            <span style={{ fontWeight: 600 }}>{p.name}</span>
                                            {isLow && stock > 0 && <span className="badge badge-warning" style={{ marginLeft: '8px', fontSize: '10px' }}>bajo stock</span>}
                                            {stock === 0 && <span className="badge badge-danger" style={{ marginLeft: '8px', fontSize: '10px' }}>sin stock</span>}
                                            {!p.isActive && <span className="badge badge-gray" style={{ marginLeft: '8px', fontSize: '10px' }}>inactivo</span>}
                                        </td>
                                        <td><span style={{ fontFamily: 'monospace', fontSize: '12px', color: 'var(--text-secondary)' }}>{p.sku || '—'}</span></td>
                                        <td style={{ textAlign: 'right', color: 'var(--text-secondary)' }}>€ {(p.costPrice || 0).toFixed(2)}</td>
                                        <td style={{ textAlign: 'right', fontWeight: 600 }}>€ {(p.salePrice || 0).toFixed(2)}</td>
                                        <td><span className="badge badge-gray">{p.vatPercent}%</span></td>
                                        <td style={{ textAlign: 'right' }}>
                                            <span style={{ fontWeight: 800, fontSize: '16px', color: isLow ? 'var(--warning)' : 'var(--text-primary)' }}>{stock}</span>
                                        </td>
                                        <td style={{ textAlign: 'right', color: 'var(--text-muted)', fontSize: '13px' }}>{reorder}</td>
                                        <td style={{ textAlign: 'right', fontWeight: 600, color: 'var(--success)' }}>
                                            € {(stock * (p.costPrice || 0)).toFixed(2)}
                                        </td>
                                        <td style={{ textAlign: 'right' }}>
                                            <div style={{ display: 'flex', gap: '6px', justifyContent: 'flex-end' }}>
                                                <button className="btn btn-success btn-sm" onClick={() => openAdjModal(p)}>Ajustar</button>
                                                <button className="btn btn-sm" style={{ background: p.isActive ? 'var(--danger-bg)' : 'var(--success-bg)', color: p.isActive ? 'var(--danger)' : 'var(--success)' }} onClick={() => toggleActive(p)}>
                                                    {p.isActive ? 'Desactivar' : 'Activar'}
                                                </button>
                                            </div>
                                        </td>
                                    </tr>
                                );
                            })}
                        </tbody>
                    </table>
                </div>
            )}

            {/* Movements Tab */}
            {tab === 'movements' && (
                <div className="erp-card" style={{ overflow: 'hidden' }}>
                    <table className="erp-table">
                        <thead><tr>
                            <th>Fecha</th><th>Producto</th><th>Almacén</th><th>Tipo</th>
                            <th style={{ textAlign: 'right' }}>Cantidad</th>
                            <th>Motivo</th>
                        </tr></thead>
                        <tbody>
                            {movements.length === 0 && (
                                <tr><td colSpan={6}><div className="empty-state"><div className="empty-state-icon">📋</div><div className="empty-state-title">Sin movimientos</div></div></td></tr>
                            )}
                            {movements.map(m => (
                                <tr key={m.id}>
                                    <td style={{ fontSize: '12px', color: 'var(--text-muted)' }}>{new Date(m.createdAt).toLocaleDateString('es-ES')}</td>
                                    <td style={{ fontWeight: 600 }}>{m.productName}</td>
                                    <td>{m.warehouseName}</td>
                                    <td><span className={`badge ${m.movementType === 'StockIn' || m.movementType === 'PurchaseReceipt' ? 'badge-success' : 'badge-danger'}`}>{m.movementType}</span></td>
                                    <td style={{ textAlign: 'right', fontWeight: 700, color: (m.quantity ?? 0) >= 0 ? 'var(--success)' : 'var(--danger)' }}>
                                        {(m.quantity ?? 0) > 0 ? '+' : ''}{m.quantity}
                                    </td>
                                    <td style={{ fontSize: '12px', color: 'var(--text-muted)' }}>{m.reason || '—'}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                    {movTotal > 20 && (
                        <div style={{ display: 'flex', justifyContent: 'center', gap: '8px', padding: '12px' }}>
                            <button className="btn btn-sm btn-secondary" disabled={movPage <= 1} onClick={() => loadMovements(movPage - 1)}>‹ Anterior</button>
                            <span style={{ fontSize: '13px', color: 'var(--text-muted)', lineHeight: '28px' }}>Página {movPage} / {movPages}</span>
                            <button className="btn btn-sm btn-secondary" disabled={movPage >= movPages} onClick={() => loadMovements(movPage + 1)}>Siguiente ›</button>
                        </div>
                    )}
                </div>
            )}

            {/* New Product Modal */}
            <AccessibleModal
                open={showModal}
                onClose={() => setShowModal(false)}
                title="Nuevo Producto"
                maxWidth="460px"
                footer={(
                    <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                        <button className="btn btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
                        <button className="btn btn-primary" onClick={handleSave} disabled={!form.name}>✓ Crear Producto</button>
                    </div>
                )}
            >
                {formError && (
                    <div className="mb-3 p-2 bg-red-50 text-red-700 border border-red-200 rounded text-sm">{formError}</div>
                )}
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '20px' }}>
                    <div className="form-group" style={{ gridColumn: 'span 2' }}>
                        <FormLabel htmlFor="prod-name" required>Nombre</FormLabel>
                        <input id="prod-name" className="erp-input" placeholder="Nombre del producto" value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} />
                    </div>
                    <div className="form-group">
                        <FormLabel htmlFor="prod-sku">SKU / referencia</FormLabel>
                        <input id="prod-sku" className="erp-input" placeholder="REF-001" value={form.sku} onChange={e => setForm({ ...form, sku: e.target.value })} />
                    </div>
                    <div className="form-group">
                        <FormLabel htmlFor="prod-price">Precio unitario (€)</FormLabel>
                        <input id="prod-price" type="number" className="erp-input" min="0" step="0.01" value={form.price} onChange={e => setForm({ ...form, price: +e.target.value })} />
                    </div>
                    <div className="form-group">
                        <label className="erp-label">TIPO DE IVA</label>
                        <select className="erp-input" value={form.taxRate} onChange={e => setForm({ ...form, taxRate: +e.target.value })}>
                            <option value={21}>IVA General 21%</option>
                            <option value={10}>IVA Reducido 10%</option>
                            <option value={4}>IVA Superreducido 4%</option>
                            <option value={0}>Exento de IVA</option>
                        </select>
                    </div>
                    <div className="form-group"><label className="erp-label">PUNTO DE REORDEN</label><input type="number" className="erp-input" min="0" value={form.reorderPoint} onChange={e => setForm({ ...form, reorderPoint: +e.target.value })} /></div>
                    <div className="form-group" style={{ gridColumn: 'span 2' }}><label className="erp-label">CANTIDAD DE REORDEN</label><input type="number" className="erp-input" min="0" value={form.reorderQty} onChange={e => setForm({ ...form, reorderQty: +e.target.value })} /></div>
                </div>
            </AccessibleModal>

            {/* Stock Adjustment Modal */}
            <AccessibleModal
                open={showAdjModal && !!adjProduct}
                onClose={() => setShowAdjModal(false)}
                title="Ajuste de Stock"
                maxWidth="400px"
                footer={(
                    <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                        <button className="btn btn-secondary" onClick={() => setShowAdjModal(false)}>Cancelar</button>
                        <button className="btn btn-primary" onClick={handleAdjust} disabled={!adjForm.warehouseId || adjForm.quantity <= 0}>✓ Aplicar Ajuste</button>
                    </div>
                )}
            >
                {adjProduct && (
                    <>
                        <p style={{ fontSize: '14px', color: 'var(--text-secondary)', marginBottom: '16px' }}>
                            Producto: <strong>{adjProduct.name}</strong> · Stock actual: <strong>{adjProduct.totalStock}</strong>
                        </p>
                        {adjError && (
                            <div className="mb-3 p-2 bg-red-50 text-red-700 border border-red-200 rounded text-sm">{adjError}</div>
                        )}
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginBottom: '20px' }}>
                            <div className="form-group">
                                <label className="erp-label">ALMACÉN *</label>
                                <select className="erp-input" value={adjForm.warehouseId} onChange={e => setAdjForm({ ...adjForm, warehouseId: e.target.value })}>
                                    <option value="">-- Seleccionar almacén --</option>
                                    {warehouses.map(w => <option key={w.id} value={w.id}>{w.name}</option>)}
                                </select>
                            </div>
                            <div className="form-group">
                                <label className="erp-label">TIPO DE MOVIMIENTO</label>
                                <select className="erp-input" value={adjForm.movementType} onChange={e => setAdjForm({ ...adjForm, movementType: e.target.value })}>
                                    <option value="StockIn">Entrada de stock</option>
                                    <option value="StockOut">Salida de stock</option>
                                    <option value="Adjustment">Ajuste de inventario</option>
                                </select>
                            </div>
                            <div className="form-group">
                                <label className="erp-label">CANTIDAD *</label>
                                <input type="number" className="erp-input" min="1" value={adjForm.quantity} onChange={e => setAdjForm({ ...adjForm, quantity: +e.target.value })} />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">MOTIVO</label>
                                <input className="erp-input" placeholder="Ej: Recepción de pedido, merma..." value={adjForm.reason} onChange={e => setAdjForm({ ...adjForm, reason: e.target.value })} />
                            </div>
                        </div>
                    </>
                )}
            </AccessibleModal>
        </PageContainer>
    );
}
