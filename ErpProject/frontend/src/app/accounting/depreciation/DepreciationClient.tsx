'use client';
import { useEffect, useState, useCallback, useRef } from 'react';
import PageContainer from '@/components/PageContainer';
import AccessibleModal from '@/components/AccessibleModal';
import { depreciationAssetSchema } from '@/lib/schemas/accountingLegacyFormSchemas';
import type { FixedAsset } from './page';

const METHODS = ['Linear', 'Declining', 'Accelerated'];
const STATUS_MAP: Record<string, { label: string; cls: string }> = {
  Active:          { label: 'Activo',              cls: 'badge-success' },
  FullyAmortized:  { label: 'Totalmente amortizado', cls: 'badge-info'    },
  Disposed:        { label: 'Dado de baja',         cls: 'badge-gray'    },
};

const emptyForm = () => ({
  assetCode: '',
  name: '',
  description: '',
  acquisitionDate: new Date().toISOString().slice(0, 10),
  commissioningDate: new Date().toISOString().slice(0, 10),
  acquisitionCost: '',
  residualValue: '0',
  usefulLifeYears: '10',
  amortizationMethod: 'Linear',
  assetAccountCode: '211',
  depreciationAccountCode: '681',
  accumDepreciationAccountCode: '281',
  notes: '',
});

interface DepreciationClientProps {
  initialAssets: FixedAsset[];
  initialStatusFilter: string;
}

export default function DepreciationClient({ initialAssets, initialStatusFilter }: DepreciationClientProps) {
  const [assets, setAssets]           = useState<FixedAsset[]>(initialAssets);
  const [loading, setLoading]         = useState(false);
  const [statusFilter, setStatusFilter] = useState(initialStatusFilter);
  const [showModal, setShowModal]     = useState(false);
  const [form, setForm]               = useState(emptyForm());
  const [saving, setSaving]           = useState(false);
  const [error, setError]             = useState<string | null>(null);

  // Depreciate modal
  const [deprModal, setDeprModal]     = useState<FixedAsset | null>(null);
  const [deprYear, setDeprYear]       = useState(new Date().getFullYear());
  const [deprMonth, setDeprMonth]     = useState(new Date().getMonth() + 1);
  const [deprLoading, setDeprLoading] = useState(false);
  const [deprResult, setDeprResult]   = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await fetch(`/api/proxy/v1/accounting/fixed-assets?status=${statusFilter}`);
      const data = await res.json();
      setAssets(Array.isArray(data) ? data : []);
    } catch {
      setAssets([]);
    } finally {
      setLoading(false);
    }
  }, [statusFilter]);

  // El primer render ya trae los datos desde el servidor (Server Component);
  // solo se refetch en el cliente cuando el filtro cambia después del montaje.
  const skipInitialLoad = useRef(true);
  useEffect(() => {
    if (skipInitialLoad.current) {
      skipInitialLoad.current = false;
      return;
    }
    load();
  }, [load]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    const parsed = depreciationAssetSchema.safeParse(form);
    if (!parsed.success) {
      setError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const res = await fetch('/api/proxy/v1/accounting/fixed-assets', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ...form,
          acquisitionCost: parseFloat(form.acquisitionCost as string),
          residualValue: parseFloat(form.residualValue as string),
          usefulLifeYears: parseInt(form.usefulLifeYears as string, 10),
        }),
      });
      if (!res.ok) {
        const err = await res.json();
        throw new Error(err.error || 'Error al crear el activo');
      }
      setShowModal(false);
      setForm(emptyForm());
      await load();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Error desconocido');
    } finally {
      setSaving(false);
    }
  };

  const handleDeprRun = async () => {
    if (!deprModal) return;
    setDeprLoading(true);
    setDeprResult(null);
    try {
      const res = await fetch(`/api/proxy/v1/accounting/fixed-assets/${deprModal.id}/depreciate`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ year: deprYear, month: deprMonth }),
      });
      const data = await res.json();
      setDeprResult(data.message);
      await load();
    } catch {
      setDeprResult('Error al procesar la amortización');
    } finally {
      setDeprLoading(false);
    }
  };

  const totalNetValue = assets.reduce((s, a) => s + a.netBookValue, 0);
  const totalCost     = assets.reduce((s, a) => s + a.acquisitionCost, 0);
  const totalAccum    = assets.reduce((s, a) => s + a.accumulatedDepreciation, 0);

  return (
    <PageContainer>
      {/* ── Header ──────────────────────────────────────────────────────────── */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
        <div>
          <h1 style={{ fontSize: 22, fontWeight: 700, margin: 0 }}>Activos Fijos y Amortización</h1>
          <p style={{ color: '#6b7280', marginTop: 4, marginBottom: 0, fontSize: 13 }}>
            Registro de inmovilizado material e intangible. Amortización lineal, degresiva y acelerada (PGC 2007 — Grupo 2).
          </p>
        </div>
        <button className="btn-primary" onClick={() => { setShowModal(true); setError(null); }}>
          + Nuevo Activo
        </button>
      </div>

      {/* ── KPIs ────────────────────────────────────────────────────────────── */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 16, marginBottom: 24 }}>
        {[
          { label: 'Coste de adquisición', value: totalCost.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' }), color: '#1d4ed8' },
          { label: 'Amortización acumulada', value: totalAccum.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' }), color: '#7c3aed' },
          { label: 'Valor neto contable', value: totalNetValue.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' }), color: '#059669' },
        ].map(kpi => (
          <div key={kpi.label} className="card" style={{ padding: '16px 20px' }}>
            <div style={{ fontSize: 12, color: '#6b7280', marginBottom: 4 }}>{kpi.label}</div>
            <div style={{ fontSize: 22, fontWeight: 700, color: kpi.color }}>{kpi.value}</div>
          </div>
        ))}
      </div>

      {/* ── Filtro ──────────────────────────────────────────────────────────── */}
      <div style={{ marginBottom: 16, display: 'flex', gap: 8 }}>
        {['Active', 'FullyAmortized', 'Disposed', ''].map(s => (
          <button
            key={s}
            onClick={() => setStatusFilter(s)}
            style={{
              padding: '6px 14px', borderRadius: 6, border: '1px solid #e5e7eb',
              background: statusFilter === s ? '#1d4ed8' : '#fff',
              color: statusFilter === s ? '#fff' : '#374151',
              fontWeight: 500, fontSize: 13, cursor: 'pointer',
            }}
          >
            {s === '' ? 'Todos' : (STATUS_MAP[s]?.label ?? s)}
          </button>
        ))}
      </div>

      {/* ── Tabla ───────────────────────────────────────────────────────────── */}
      {loading ? (
        <div style={{ padding: 40, textAlign: 'center', color: '#9ca3af' }}>Cargando activos...</div>
      ) : assets.length === 0 ? (
        <div style={{ padding: 40, textAlign: 'center', color: '#9ca3af' }}>
          No hay activos registrados. Crea el primero con el botón &quot;+ Nuevo Activo&quot;.
        </div>
      ) : (
        <div className="card" style={{ overflowX: 'auto' }}>
          <table className="erp-table" style={{ width: '100%' }}>
            <thead>
              <tr>
                <th>Código</th>
                <th>Descripción</th>
                <th>Fecha alta</th>
                <th>Coste</th>
                <th>Amort. acum.</th>
                <th>Valor neto</th>
                <th>Cuota/mes</th>
                <th>Método</th>
                <th>Estado</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {assets.map(a => (
                <tr key={a.id}>
                  <td><span style={{ fontFamily: 'monospace', fontWeight: 600 }}>{a.assetCode}</span></td>
                  <td>
                    <div style={{ fontWeight: 600 }}>{a.name}</div>
                    {a.description && <div style={{ fontSize: 11, color: '#9ca3af' }}>{a.description}</div>}
                  </td>
                  <td style={{ whiteSpace: 'nowrap' }}>{new Date(a.commissioningDate).toLocaleDateString('es-ES')}</td>
                  <td style={{ textAlign: 'right' }}>{a.acquisitionCost.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' })}</td>
                  <td style={{ textAlign: 'right', color: '#7c3aed' }}>
                    {a.accumulatedDepreciation.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' })}
                  </td>
                  <td style={{ textAlign: 'right', fontWeight: 600, color: a.netBookValue > 0 ? '#059669' : '#6b7280' }}>
                    {a.netBookValue.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' })}
                  </td>
                  <td style={{ textAlign: 'right', fontSize: 12 }}>
                    {a.monthlyDepreciation.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' })}
                  </td>
                  <td>
                    <span style={{ fontSize: 11, background: '#f3f4f6', padding: '2px 8px', borderRadius: 4 }}>
                      {a.amortizationMethod}
                    </span>
                  </td>
                  <td>
                    <span className={STATUS_MAP[a.status]?.cls ?? 'badge-gray'}>
                      {STATUS_MAP[a.status]?.label ?? a.status}
                    </span>
                  </td>
                  <td>
                    {a.status === 'Active' && (
                      <button
                        className="btn-secondary"
                        style={{ fontSize: 11, padding: '4px 10px' }}
                        onClick={() => { setDeprModal(a); setDeprResult(null); }}
                      >
                        Amortizar
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* ── Modal Nuevo Activo ───────────────────────────────────────────────── */}
      <AccessibleModal open={showModal} onClose={() => setShowModal(false)} title="Registrar Activo Fijo" maxWidth="680px">
        <form onSubmit={handleCreate}>
              {error && (
                <div style={{ background: '#fee2e2', color: '#991b1b', padding: '8px 12px', borderRadius: 6, marginBottom: 16, fontSize: 13 }}>
                  {error}
                </div>
              )}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                <div className="form-group">
                  <label className="erp-label">CÓDIGO *</label>
                  <input className="erp-input" required value={form.assetCode}
                    onChange={e => setForm({ ...form, assetCode: e.target.value })}
                    placeholder="VEH-001" />
                </div>
                <div className="form-group">
                  <label className="erp-label">NOMBRE *</label>
                  <input className="erp-input" required value={form.name}
                    onChange={e => setForm({ ...form, name: e.target.value })}
                    placeholder="Vehículo comercial" />
                </div>
                <div className="form-group" style={{ gridColumn: 'span 2' }}>
                  <label className="erp-label">DESCRIPCIÓN</label>
                  <input className="erp-input" value={form.description}
                    onChange={e => setForm({ ...form, description: e.target.value })}
                    placeholder="Descripción opcional" />
                </div>
                <div className="form-group">
                  <label className="erp-label">FECHA ADQUISICIÓN *</label>
                  <input className="erp-input" type="date" required value={form.acquisitionDate}
                    onChange={e => setForm({ ...form, acquisitionDate: e.target.value })} />
                </div>
                <div className="form-group">
                  <label className="erp-label">FECHA PUESTA EN FUNCIONAMIENTO *</label>
                  <input className="erp-input" type="date" required value={form.commissioningDate}
                    onChange={e => setForm({ ...form, commissioningDate: e.target.value })} />
                </div>
                <div className="form-group">
                  <label className="erp-label">COSTE ADQUISICIÓN (€) *</label>
                  <input className="erp-input" type="number" step="0.01" min="0" required value={form.acquisitionCost}
                    onChange={e => setForm({ ...form, acquisitionCost: e.target.value })}
                    placeholder="25000.00" />
                </div>
                <div className="form-group">
                  <label className="erp-label">VALOR RESIDUAL (€)</label>
                  <input className="erp-input" type="number" step="0.01" min="0" value={form.residualValue}
                    onChange={e => setForm({ ...form, residualValue: e.target.value })} />
                </div>
                <div className="form-group">
                  <label className="erp-label">VIDA ÚTIL (AÑOS) *</label>
                  <input className="erp-input" type="number" min="1" max="100" required value={form.usefulLifeYears}
                    onChange={e => setForm({ ...form, usefulLifeYears: e.target.value })} />
                </div>
                <div className="form-group">
                  <label className="erp-label">MÉTODO AMORTIZACIÓN</label>
                  <select className="erp-input" value={form.amortizationMethod}
                    onChange={e => setForm({ ...form, amortizationMethod: e.target.value })}>
                    {METHODS.map(m => <option key={m} value={m}>{m}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label className="erp-label">CTA. INMOVILIZADO (2xx)</label>
                  <input className="erp-input" value={form.assetAccountCode}
                    onChange={e => setForm({ ...form, assetAccountCode: e.target.value })}
                    placeholder="211" />
                </div>
                <div className="form-group">
                  <label className="erp-label">CTA. DOTACIÓN AMORT. (68x)</label>
                  <input className="erp-input" value={form.depreciationAccountCode}
                    onChange={e => setForm({ ...form, depreciationAccountCode: e.target.value })}
                    placeholder="681" />
                </div>
                <div className="form-group">
                  <label className="erp-label">CTA. AMORT. ACUMULADA (28x)</label>
                  <input className="erp-input" value={form.accumDepreciationAccountCode}
                    onChange={e => setForm({ ...form, accumDepreciationAccountCode: e.target.value })}
                    placeholder="281" />
                </div>
                <div className="form-group">
                  <label className="erp-label">NOTAS</label>
                  <input className="erp-input" value={form.notes}
                    onChange={e => setForm({ ...form, notes: e.target.value })} />
                </div>
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12, marginTop: 20, paddingTop: 16, borderTop: '1px solid #f3f4f6' }}>
                <button type="button" className="btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
                <button type="submit" className="btn-primary" disabled={saving}>
                  {saving ? 'Guardando...' : 'Registrar Activo'}
                </button>
              </div>
            </form>
      </AccessibleModal>

      <AccessibleModal open={!!deprModal} onClose={() => setDeprModal(null)} title="Dotación de Amortización" maxWidth="460px"
        footer={deprModal ? (<div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12 }}>
          <button className="btn-secondary" onClick={() => setDeprModal(null)}>Cerrar</button>
          <button className="btn-primary" onClick={handleDeprRun} disabled={deprLoading}>
            {deprLoading ? 'Procesando...' : 'Generar Asiento'}
          </button>
        </div>) : undefined}>
        {deprModal && (<>
              <div style={{ background: '#f9fafb', borderRadius: 8, padding: '12px 16px', marginBottom: 20 }}>
                <div style={{ fontWeight: 600 }}>{deprModal.name} <span style={{ color: '#9ca3af', fontWeight: 400 }}>({deprModal.assetCode})</span></div>
                <div style={{ fontSize: 13, color: '#6b7280', marginTop: 4 }}>
                  Cuota mensual: <strong>{deprModal.monthlyDepreciation.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' })}</strong>
                  {' '}| Valor neto: <strong>{deprModal.netBookValue.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' })}</strong>
                </div>
              </div>
              <p style={{ fontSize: 13, color: '#374151', marginBottom: 16 }}>
                Genera el asiento de dotación (Debe {deprModal.depreciationAccountCode} / Haber {deprModal.accumDepreciationAccountCode}) para el mes indicado.
                El proceso es idempotente: no genera duplicados si ya existe el asiento.
              </p>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 20 }}>
                <div className="form-group">
                  <label className="erp-label">AÑO</label>
                  <input className="erp-input" type="number" min="2000" max="2100"
                    value={deprYear} onChange={e => setDeprYear(parseInt(e.target.value))} />
                </div>
                <div className="form-group">
                  <label className="erp-label">MES</label>
                  <select className="erp-input" value={deprMonth} onChange={e => setDeprMonth(parseInt(e.target.value))}>
                    {Array.from({ length: 12 }, (_, i) => i + 1).map(m => (
                      <option key={m} value={m}>{m.toString().padStart(2, '0')}</option>
                    ))}
                  </select>
                </div>
              </div>
              {deprResult && (
                <div style={{
                  background: deprResult.startsWith('Error') ? '#fee2e2' : '#d1fae5',
                  color: deprResult.startsWith('Error') ? '#991b1b' : '#065f46',
                  padding: '10px 14px', borderRadius: 6, marginBottom: 16, fontSize: 13
                }}>
                  {deprResult}
                </div>
              )}
        </>)}
      </AccessibleModal>
    </PageContainer>
  );
}
