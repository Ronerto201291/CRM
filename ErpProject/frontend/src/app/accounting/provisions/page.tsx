'use client';
import { useEffect, useState, useCallback } from 'react';
import PageContainer from '@/components/PageContainer';

interface Provision {
  id: string;
  code: string;
  description: string;
  amount: number;
  dueDate: string;
  status: string;
  linkedJournalEntryId?: string;
  createdAt: string;
}

const PROVISION_CODES = [
  { code: '490', label: '490 — Deterioro de créditos por insolvencias (clientes)' },
  { code: '499', label: '499 — Provisión para otras operaciones comerciales' },
  { code: '147', label: '147 — Provisión para impuestos' },
  { code: '140', label: '140 — Provisión para retribuciones al personal' },
  { code: '142', label: '142 — Provisión para responsabilidades' },
  { code: '145', label: '145 — Provisión para actuaciones medioambientales' },
];

const STATUS_MAP: Record<string, { label: string; cls: string }> = {
  Active:   { label: 'Activa',   cls: 'badge-success' },
  Released: { label: 'Liberada', cls: 'badge-gray'    },
  Expired:  { label: 'Expirada', cls: 'badge-warning' },
};

const emptyForm = () => ({
  code: '490',
  description: '',
  amount: '',
  dueDate: new Date(new Date().setMonth(new Date().getMonth() + 3)).toISOString().slice(0, 10),
  notes: '',
});

export default function ProvisionsPage() {
  const [provisions, setProvisions]     = useState<Provision[]>([]);
  const [loading, setLoading]           = useState(true);
  const [statusFilter, setStatusFilter] = useState('Active');
  const [showModal, setShowModal]       = useState(false);
  const [form, setForm]                 = useState(emptyForm());
  const [saving, setSaving]             = useState(false);
  const [error, setError]               = useState<string | null>(null);
  const [releaseId, setReleaseId]       = useState<string | null>(null);
  const [releaseLoading, setReleaseLoading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const qs = statusFilter ? `?status=${statusFilter}` : '';
      const res  = await fetch(`/api/proxy/v1/accounting/provisions${qs}`);
      const data = await res.json();
      setProvisions(Array.isArray(data) ? data : []);
    } catch {
      setProvisions([]);
    } finally {
      setLoading(false);
    }
  }, [statusFilter]);

  useEffect(() => { load(); }, [load]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError(null);
    try {
      const res = await fetch('/api/proxy/v1/accounting/provisions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          code: form.code,
          description: form.description,
          amount: parseFloat(form.amount as string),
          dueDate: form.dueDate,
          notes: form.notes || null,
        }),
      });
      if (!res.ok) {
        const err = await res.json();
        throw new Error(err.error || 'Error al crear la provisión');
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

  const handleRelease = async (id: string) => {
    setReleaseLoading(true);
    try {
      const res = await fetch(`/api/proxy/v1/accounting/provisions/${id}/release`, {
        method: 'POST',
      });
      if (!res.ok) {
        const err = await res.json();
        throw new Error(err.error || 'Error al liberar');
      }
      setReleaseId(null);
      await load();
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'Error al liberar la provisión');
    } finally {
      setReleaseLoading(false);
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('¿Eliminar esta provisión? Solo se pueden eliminar provisiones sin asiento contable.')) return;
    try {
      const res = await fetch(`/api/proxy/v1/accounting/provisions/${id}`, { method: 'DELETE' });
      if (!res.ok) {
        const err = await res.json();
        throw new Error(err.error);
      }
      await load();
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'Error al eliminar');
    }
  };

  const totalActive = provisions
    .filter(p => p.status === 'Active')
    .reduce((s, p) => s + p.amount, 0);

  return (
    <PageContainer>
      {/* ── Header ──────────────────────────────────────────────────────────── */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
        <div>
          <h1 style={{ fontSize: 22, fontWeight: 700, margin: 0 }}>Provisiones Contables</h1>
          <p style={{ color: '#6b7280', marginTop: 4, marginBottom: 0, fontSize: 13 }}>
            Gestión de provisiones PGC 2007 (Grupos 14 y 49). El asiento de dotación y liberación se generan automáticamente.
          </p>
        </div>
        <button className="btn-primary" onClick={() => { setShowModal(true); setError(null); setForm(emptyForm()); }}>
          + Nueva Provisión
        </button>
      </div>

      {/* ── KPIs ────────────────────────────────────────────────────────────── */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 16, marginBottom: 24 }}>
        {[
          { label: 'Provisiones activas', value: provisions.filter(p => p.status === 'Active').length, color: '#1d4ed8', fmt: false },
          { label: 'Total dotado activo', value: totalActive, color: '#7c3aed', fmt: true },
          { label: 'Provisiones liberadas', value: provisions.filter(p => p.status === 'Released').length, color: '#059669', fmt: false },
        ].map(kpi => (
          <div key={kpi.label} className="card" style={{ padding: '16px 20px' }}>
            <div style={{ fontSize: 12, color: '#6b7280', marginBottom: 4 }}>{kpi.label}</div>
            <div style={{ fontSize: 22, fontWeight: 700, color: kpi.color }}>
              {kpi.fmt
                ? (kpi.value as number).toLocaleString('es-ES', { style: 'currency', currency: 'EUR' })
                : kpi.value}
            </div>
          </div>
        ))}
      </div>

      {/* ── Filtros ─────────────────────────────────────────────────────────── */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
        {['Active', 'Released', 'Expired', ''].map(s => (
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
            {s === '' ? 'Todas' : (STATUS_MAP[s]?.label ?? s)}
          </button>
        ))}
      </div>

      {/* ── Tabla ───────────────────────────────────────────────────────────── */}
      {loading ? (
        <div style={{ padding: 40, textAlign: 'center', color: '#9ca3af' }}>Cargando provisiones...</div>
      ) : provisions.length === 0 ? (
        <div style={{ padding: 40, textAlign: 'center', color: '#9ca3af' }}>
          No hay provisiones {statusFilter ? `en estado "${STATUS_MAP[statusFilter]?.label}"` : ''}.
        </div>
      ) : (
        <div className="card" style={{ overflowX: 'auto' }}>
          <table className="erp-table" style={{ width: '100%' }}>
            <thead>
              <tr>
                <th>Cuenta PGC</th>
                <th>Descripción</th>
                <th>Importe</th>
                <th>Vencimiento</th>
                <th>Estado</th>
                <th>Asiento</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {provisions.map(p => (
                <tr key={p.id}>
                  <td>
                    <span style={{ fontFamily: 'monospace', fontWeight: 700, color: '#1d4ed8' }}>{p.code}</span>
                  </td>
                  <td>{p.description}</td>
                  <td style={{ textAlign: 'right', fontWeight: 600 }}>
                    {p.amount.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' })}
                  </td>
                  <td style={{ whiteSpace: 'nowrap' }}>
                    {new Date(p.dueDate).toLocaleDateString('es-ES')}
                    {new Date(p.dueDate) < new Date() && p.status === 'Active' && (
                      <span style={{ marginLeft: 6, fontSize: 11, color: '#dc2626' }}>⚠ vencida</span>
                    )}
                  </td>
                  <td>
                    <span className={STATUS_MAP[p.status]?.cls ?? 'badge-gray'}>
                      {STATUS_MAP[p.status]?.label ?? p.status}
                    </span>
                  </td>
                  <td style={{ fontSize: 12 }}>
                    {p.linkedJournalEntryId
                      ? <span style={{ color: '#059669' }}>✓ Generado</span>
                      : <span style={{ color: '#9ca3af' }}>Sin asiento</span>}
                  </td>
                  <td>
                    <div style={{ display: 'flex', gap: 6 }}>
                      {p.status === 'Active' && (
                        <button
                          className="btn-secondary"
                          style={{ fontSize: 11, padding: '3px 10px', color: '#7c3aed', borderColor: '#7c3aed' }}
                          onClick={() => setReleaseId(p.id)}
                        >
                          Liberar
                        </button>
                      )}
                      {!p.linkedJournalEntryId && (
                        <button
                          className="btn-secondary"
                          style={{ fontSize: 11, padding: '3px 10px', color: '#dc2626', borderColor: '#dc2626' }}
                          onClick={() => handleDelete(p.id)}
                        >
                          Eliminar
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* ── Modal Nueva Provisión ───────────────────────────────────────────── */}
      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal" style={{ width: 520 }} onClick={e => e.stopPropagation()}>
            <div className="modal-header">
              <h2>Nueva Provisión Contable</h2>
              <button className="modal-close" onClick={() => setShowModal(false)}>✕</button>
            </div>
            <form onSubmit={handleCreate} style={{ padding: '20px 24px' }}>
              {error && (
                <div style={{ background: '#fee2e2', color: '#991b1b', padding: '8px 12px', borderRadius: 6, marginBottom: 16, fontSize: 13 }}>
                  {error}
                </div>
              )}
              <div style={{ background: '#eff6ff', borderRadius: 8, padding: '10px 14px', marginBottom: 16, fontSize: 12, color: '#1d4ed8' }}>
                El asiento de dotación se generará automáticamente si las cuentas contables PGC existen en el plan de cuentas.
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                <div className="form-group" style={{ gridColumn: 'span 2' }}>
                  <label className="erp-label">CUENTA PGC *</label>
                  <select className="erp-input" required value={form.code}
                    onChange={e => setForm({ ...form, code: e.target.value })}>
                    {PROVISION_CODES.map(c => (
                      <option key={c.code} value={c.code}>{c.label}</option>
                    ))}
                  </select>
                </div>
                <div className="form-group" style={{ gridColumn: 'span 2' }}>
                  <label className="erp-label">DESCRIPCIÓN *</label>
                  <input className="erp-input" required value={form.description}
                    onChange={e => setForm({ ...form, description: e.target.value })}
                    placeholder="Ej: Provisión por litigio con cliente Empresa SA" />
                </div>
                <div className="form-group">
                  <label className="erp-label">IMPORTE (€) *</label>
                  <input className="erp-input" type="number" step="0.01" min="0.01" required value={form.amount}
                    onChange={e => setForm({ ...form, amount: e.target.value })}
                    placeholder="5000.00" />
                </div>
                <div className="form-group">
                  <label className="erp-label">FECHA VENCIMIENTO *</label>
                  <input className="erp-input" type="date" required value={form.dueDate}
                    onChange={e => setForm({ ...form, dueDate: e.target.value })} />
                </div>
                <div className="form-group" style={{ gridColumn: 'span 2' }}>
                  <label className="erp-label">NOTAS</label>
                  <input className="erp-input" value={form.notes}
                    onChange={e => setForm({ ...form, notes: e.target.value })}
                    placeholder="Referencia expediente, juzgado, etc." />
                </div>
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12, marginTop: 20, paddingTop: 16, borderTop: '1px solid #f3f4f6' }}>
                <button type="button" className="btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
                <button type="submit" className="btn-primary" disabled={saving}>
                  {saving ? 'Guardando...' : 'Crear Provisión'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ── Confirmar Liberación ────────────────────────────────────────────── */}
      {releaseId && (
        <div className="modal-overlay" onClick={() => setReleaseId(null)}>
          <div className="modal" style={{ width: 420 }} onClick={e => e.stopPropagation()}>
            <div className="modal-header">
              <h2>Liberar Provisión</h2>
              <button className="modal-close" onClick={() => setReleaseId(null)}>✕</button>
            </div>
            <div style={{ padding: '20px 24px' }}>
              <p style={{ fontSize: 14, color: '#374151' }}>
                Al liberar la provisión se generará el asiento contable inverso:
              </p>
              <div style={{ background: '#f9fafb', borderRadius: 6, padding: '10px 14px', margin: '12px 0', fontSize: 13 }}>
                <div>Debe → Cuenta de provisión (490/499/147…)</div>
                <div>Haber → 795 Exceso de provisiones</div>
              </div>
              <p style={{ fontSize: 13, color: '#6b7280' }}>Esta acción no se puede deshacer.</p>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12, marginTop: 20 }}>
                <button className="btn-secondary" onClick={() => setReleaseId(null)}>Cancelar</button>
                <button
                  className="btn-primary"
                  style={{ background: '#7c3aed', borderColor: '#7c3aed' }}
                  disabled={releaseLoading}
                  onClick={() => handleRelease(releaseId)}
                >
                  {releaseLoading ? 'Liberando...' : 'Liberar y Contabilizar'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </PageContainer>
  );
}
