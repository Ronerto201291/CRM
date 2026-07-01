'use client';
import { useEffect, useState, useCallback } from 'react';
import PageContainer from '@/components/PageContainer';

interface BudgetSummary {
  id: string;
  name: string;
  fiscalYear: number;
  startDate: string;
  endDate: string;
  status: string;
  linesCount: number;
  totalBudgeted: number;
  createdAt: string;
}

interface BudgetDetail {
  id: string;
  name: string;
  fiscalYear: number;
  startDate: string;
  endDate: string;
  status: string;
  lines: BudgetLine[];
}

interface BudgetLine {
  id: string;
  budgetId: string;
  accountId?: string;
  accountCode?: string;
  costCenterId?: string;
  type: string;
  budgetedAmount: number;
}

interface BudgetLineAnalysis {
  lineId: string;
  accountCode?: string;
  type: string;
  budgeted: number;
  actual: number;
  variance: number;
  variancePercent: number;
  status: string;  // OnTrack | OverBudget | UnderBudget
}

const STATUS_MAP: Record<string, { label: string; cls: string }> = {
  Draft:    { label: 'Borrador', cls: 'badge-gray'    },
  Approved: { label: 'Aprobado', cls: 'badge-info'    },
  Active:   { label: 'Activo',   cls: 'badge-success' },
  Closed:   { label: 'Cerrado',  cls: 'badge-gray'    },
};

const ANALYSIS_MAP: Record<string, { label: string; color: string }> = {
  OnTrack:     { label: 'En línea',        color: '#059669' },
  OverBudget:  { label: 'Sobre presupuesto', color: '#dc2626' },
  UnderBudget: { label: 'Bajo presupuesto', color: '#f59e0b' },
};

export default function BudgetsPage() {
  const [budgets, setBudgets]           = useState<BudgetSummary[]>([]);
  const [loading, setLoading]           = useState(true);
  const [yearFilter, setYearFilter]     = useState(new Date().getFullYear());
  const [showCreate, setShowCreate]     = useState(false);
  const [name, setName]                 = useState('');
  const [year, setYear]                 = useState(new Date().getFullYear());
  const [saving, setSaving]             = useState(false);

  // Detail view
  const [detail, setDetail]             = useState<BudgetDetail | null>(null);
  const [analysis, setAnalysis]         = useState<BudgetLineAnalysis[] | null>(null);
  const [analysisLoading, setAnalysisLoading] = useState(false);
  const [detailLoading, setDetailLoading]     = useState(false);

  // Add line form
  const [showAddLine, setShowAddLine]   = useState(false);
  const [lineForm, setLineForm]         = useState({
    accountId: '', accountCode: '', type: 'Expense', budgetedAmount: ''
  });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res  = await fetch(`/api/proxy/v1/accounting/budgets?fiscalYear=${yearFilter}`);
      const data = await res.json();
      setBudgets(Array.isArray(data) ? data : []);
    } catch {
      setBudgets([]);
    } finally {
      setLoading(false);
    }
  }, [yearFilter]);

  useEffect(() => { load(); }, [load]);

  const openDetail = useCallback(async (id: string) => {
    setDetail(null);
    setAnalysis(null);
    setDetailLoading(true);
    try {
      const res  = await fetch(`/api/proxy/v1/accounting/budgets/${id}`);
      const data = await res.json();
      setDetail(data);
    } finally {
      setDetailLoading(false);
    }
  }, []);

  const loadAnalysis = async (id: string) => {
    setAnalysisLoading(true);
    setAnalysis(null);
    try {
      const res  = await fetch(`/api/proxy/v1/accounting/budgets/${id}/analysis`);
      const data = await res.json();
      setAnalysis(Array.isArray(data) ? data : []);
    } finally {
      setAnalysisLoading(false);
    }
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      const res = await fetch('/api/proxy/v1/accounting/budgets', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name,
          fiscalYear: year,
          startDate: `${year}-01-01T00:00:00Z`,
          endDate:   `${year}-12-31T23:59:59Z`,
        }),
      });
      if (!res.ok) throw new Error('Error al crear presupuesto');
      setShowCreate(false);
      setName('');
      await load();
    } finally {
      setSaving(false);
    }
  };

  const handleApprove = async (id: string) => {
    if (!confirm('¿Aprobar este presupuesto? Pasará de Borrador a Aprobado.')) return;
    await fetch(`/api/proxy/v1/accounting/budgets/${id}/approve`, { method: 'POST' });
    await load();
    if (detail?.id === id) openDetail(id);
  };

  const handleClose = async (id: string) => {
    if (!confirm('¿Cerrar el presupuesto? No podrá añadir más líneas.')) return;
    await fetch(`/api/proxy/v1/accounting/budgets/${id}/close`, { method: 'POST' });
    await load();
    if (detail?.id === id) openDetail(id);
  };

  const handleAddLine = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!detail) return;
    setSaving(true);
    try {
      const res = await fetch(`/api/proxy/v1/accounting/budgets/${detail.id}/lines`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          accountId: lineForm.accountId || null,
          accountCode: lineForm.accountCode || null,
          type: lineForm.type,
          budgetedAmount: parseFloat(lineForm.budgetedAmount),
        }),
      });
      if (!res.ok) throw new Error('Error al añadir línea');
      setShowAddLine(false);
      setLineForm({ accountId: '', accountCode: '', type: 'Expense', budgetedAmount: '' });
      openDetail(detail.id);
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteLine = async (lineId: string) => {
    if (!confirm('¿Eliminar esta línea?')) return;
    await fetch(`/api/proxy/v1/accounting/budgets/lines/${lineId}`, { method: 'DELETE' });
    if (detail) openDetail(detail.id);
  };

  const totalBudgeted = analysis?.reduce((s, l) => s + l.budgeted, 0) ?? 0;
  const totalActual   = analysis?.reduce((s, l) => s + l.actual,   0) ?? 0;

  return (
    <PageContainer>
      <div style={{ display: 'flex', gap: 24 }}>
        {/* ── Panel izquierdo: lista de presupuestos ─────────────────────── */}
        <div style={{ width: 360, flexShrink: 0 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
            <div>
              <h1 style={{ fontSize: 20, fontWeight: 700, margin: 0 }}>Presupuestos</h1>
              <p style={{ fontSize: 12, color: '#6b7280', marginTop: 4, marginBottom: 0 }}>Presupuesto vs Real (Budget vs Actual)</p>
            </div>
            <button className="btn-primary" style={{ fontSize: 13 }} onClick={() => setShowCreate(true)}>
              + Nuevo
            </button>
          </div>

          {/* Filtro año */}
          <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
            {[new Date().getFullYear() - 1, new Date().getFullYear(), new Date().getFullYear() + 1].map(y => (
              <button
                key={y}
                onClick={() => setYearFilter(y)}
                style={{
                  padding: '5px 12px', borderRadius: 6, border: '1px solid #e5e7eb',
                  background: yearFilter === y ? '#1d4ed8' : '#fff',
                  color: yearFilter === y ? '#fff' : '#374151',
                  fontWeight: 500, fontSize: 12, cursor: 'pointer',
                }}
              >
                {y}
              </button>
            ))}
          </div>

          {loading ? (
            <div style={{ color: '#9ca3af', padding: 20, textAlign: 'center' }}>Cargando...</div>
          ) : budgets.length === 0 ? (
            <div style={{ color: '#9ca3af', padding: 20, textAlign: 'center', fontSize: 13 }}>
              No hay presupuestos para {yearFilter}
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {budgets.map(b => (
                <div
                  key={b.id}
                  className="card"
                  style={{
                    padding: '14px 16px', cursor: 'pointer',
                    border: detail?.id === b.id ? '2px solid #1d4ed8' : '1px solid #e5e7eb',
                    borderRadius: 8,
                  }}
                  onClick={() => openDetail(b.id)}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                    <div style={{ fontWeight: 600, fontSize: 14 }}>{b.name}</div>
                    <span className={STATUS_MAP[b.status]?.cls ?? 'badge-gray'}>
                      {STATUS_MAP[b.status]?.label ?? b.status}
                    </span>
                  </div>
                  <div style={{ fontSize: 12, color: '#6b7280', marginTop: 6 }}>
                    Ejercicio {b.fiscalYear} · {b.linesCount} líneas
                  </div>
                  <div style={{ fontSize: 13, fontWeight: 600, color: '#374151', marginTop: 4 }}>
                    {b.totalBudgeted.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' })} presupuestado
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* ── Panel derecho: detalle + análisis ─────────────────────────── */}
        <div style={{ flex: 1, minWidth: 0 }}>
          {detailLoading && (
            <div style={{ padding: 40, textAlign: 'center', color: '#9ca3af' }}>Cargando presupuesto...</div>
          )}

          {!detail && !detailLoading && (
            <div style={{ padding: 60, textAlign: 'center', color: '#d1d5db' }}>
              <div style={{ fontSize: 48, marginBottom: 12 }}>📊</div>
              <div style={{ fontSize: 14 }}>Selecciona un presupuesto para ver el detalle</div>
            </div>
          )}

          {detail && (
            <>
              {/* Header detalle */}
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 20 }}>
                <div>
                  <h2 style={{ fontSize: 18, fontWeight: 700, margin: 0 }}>{detail.name}</h2>
                  <div style={{ fontSize: 12, color: '#6b7280', marginTop: 4 }}>
                    {detail.fiscalYear} · {new Date(detail.startDate).toLocaleDateString('es-ES')} — {new Date(detail.endDate).toLocaleDateString('es-ES')}
                  </div>
                </div>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                  <span className={STATUS_MAP[detail.status]?.cls ?? 'badge-gray'}>
                    {STATUS_MAP[detail.status]?.label ?? detail.status}
                  </span>
                  {detail.status === 'Draft' && (
                    <button className="btn-secondary" style={{ fontSize: 12 }} onClick={() => handleApprove(detail.id)}>
                      Aprobar
                    </button>
                  )}
                  {detail.status !== 'Closed' && (
                    <button className="btn-secondary" style={{ fontSize: 12 }} onClick={() => handleClose(detail.id)}>
                      Cerrar
                    </button>
                  )}
                  <button
                    className="btn-primary"
                    style={{ fontSize: 12 }}
                    onClick={() => loadAnalysis(detail.id)}
                    disabled={analysisLoading}
                  >
                    {analysisLoading ? 'Calculando...' : '📊 Analizar vs Real'}
                  </button>
                </div>
              </div>

              {/* ── Análisis presupuesto vs real ─────────────────────────── */}
              {analysis && (
                <div className="card" style={{ padding: '16px 20px', marginBottom: 20 }}>
                  <h3 style={{ fontSize: 14, fontWeight: 600, margin: '0 0 16px 0' }}>Análisis Presupuesto vs Real</h3>

                  {/* Totales */}
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12, marginBottom: 16 }}>
                    {[
                      { label: 'Total presupuestado', value: totalBudgeted, color: '#1d4ed8' },
                      { label: 'Total real (contabilizado)', value: totalActual, color: '#374151' },
                      {
                        label: 'Variación', value: totalBudgeted - totalActual,
                        color: (totalBudgeted - totalActual) >= 0 ? '#059669' : '#dc2626'
                      },
                    ].map(kpi => (
                      <div key={kpi.label} style={{ background: '#f9fafb', borderRadius: 6, padding: '10px 14px' }}>
                        <div style={{ fontSize: 11, color: '#6b7280' }}>{kpi.label}</div>
                        <div style={{ fontSize: 16, fontWeight: 700, color: kpi.color }}>
                          {kpi.value.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' })}
                        </div>
                      </div>
                    ))}
                  </div>

                  {analysis.length === 0 ? (
                    <div style={{ color: '#9ca3af', fontSize: 13, textAlign: 'center', padding: 12 }}>
                      No hay líneas de presupuesto o no hay asientos contabilizados en este período.
                    </div>
                  ) : (
                    <table className="erp-table" style={{ width: '100%' }}>
                      <thead>
                        <tr>
                          <th>Tipo</th>
                          <th style={{ textAlign: 'right' }}>Presupuestado</th>
                          <th style={{ textAlign: 'right' }}>Real</th>
                          <th style={{ textAlign: 'right' }}>Variación</th>
                          <th style={{ textAlign: 'right' }}>%</th>
                          <th>Estado</th>
                        </tr>
                      </thead>
                      <tbody>
                        {analysis.map(a => {
                          const st = ANALYSIS_MAP[a.status] ?? { label: a.status, color: '#374151' };
                          return (
                            <tr key={a.lineId}>
                              <td>
                                <span style={{
                                  fontSize: 11,
                                  background: a.type === 'Revenue' ? '#d1fae5' : '#fee2e2',
                                  color: a.type === 'Revenue' ? '#065f46' : '#991b1b',
                                  padding: '2px 8px', borderRadius: 4
                                }}>
                                  {a.type === 'Revenue' ? 'Ingreso' : 'Gasto'}
                                </span>
                              </td>
                              <td style={{ textAlign: 'right' }}>
                                {a.budgeted.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' })}
                              </td>
                              <td style={{ textAlign: 'right' }}>
                                {a.actual.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' })}
                              </td>
                              <td style={{ textAlign: 'right', fontWeight: 600, color: a.variance >= 0 ? '#059669' : '#dc2626' }}>
                                {a.variance >= 0 ? '+' : ''}{a.variance.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' })}
                              </td>
                              <td style={{ textAlign: 'right', fontSize: 12 }}>
                                {a.variancePercent}%
                              </td>
                              <td>
                                <span style={{ fontSize: 11, color: st.color, fontWeight: 600 }}>
                                  {st.label}
                                </span>
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  )}
                </div>
              )}

              {/* ── Líneas del presupuesto ─────────────────────────────── */}
              <div className="card" style={{ padding: '16px 20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
                  <h3 style={{ fontSize: 14, fontWeight: 600, margin: 0 }}>Líneas del Presupuesto</h3>
                  {detail.status !== 'Closed' && (
                    <button className="btn-secondary" style={{ fontSize: 12 }} onClick={() => setShowAddLine(true)}>
                      + Añadir Línea
                    </button>
                  )}
                </div>
                {detail.lines.length === 0 ? (
                  <div style={{ color: '#9ca3af', fontSize: 13, padding: 12, textAlign: 'center' }}>
                    Sin líneas. Añade ingresos y gastos presupuestados para poder analizar.
                  </div>
                ) : (
                  <table className="erp-table" style={{ width: '100%' }}>
                    <thead>
                      <tr>
                        <th>Tipo</th>
                        <th>Cuenta</th>
                        <th style={{ textAlign: 'right' }}>Importe</th>
                        <th></th>
                      </tr>
                    </thead>
                    <tbody>
                      {detail.lines.map(l => (
                        <tr key={l.id}>
                          <td>
                            <span style={{
                              fontSize: 11,
                              background: l.type === 'Revenue' ? '#d1fae5' : '#fee2e2',
                              color: l.type === 'Revenue' ? '#065f46' : '#991b1b',
                              padding: '2px 8px', borderRadius: 4
                            }}>
                              {l.type === 'Revenue' ? 'Ingreso' : 'Gasto'}
                            </span>
                          </td>
                          <td style={{ fontFamily: 'monospace', fontSize: 13 }}>
                            {l.accountCode ?? '—'}
                          </td>
                          <td style={{ textAlign: 'right', fontWeight: 600 }}>
                            {l.budgetedAmount.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' })}
                          </td>
                          <td>
                            {detail.status !== 'Closed' && (
                              <button
                                style={{ background: 'none', border: 'none', color: '#dc2626', cursor: 'pointer', fontSize: 14 }}
                                onClick={() => handleDeleteLine(l.id)}
                              >
                                ✕
                              </button>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            </>
          )}
        </div>
      </div>

      {/* ── Modal Crear Presupuesto ──────────────────────────────────────────── */}
      {showCreate && (
        <div className="modal-overlay" onClick={() => setShowCreate(false)}>
          <div className="modal" style={{ width: 420 }} onClick={e => e.stopPropagation()}>
            <div className="modal-header">
              <h2>Nuevo Presupuesto</h2>
              <button className="modal-close" onClick={() => setShowCreate(false)}>✕</button>
            </div>
            <form onSubmit={handleCreate} style={{ padding: '20px 24px' }}>
              <div className="form-group">
                <label className="erp-label">NOMBRE *</label>
                <input className="erp-input" required value={name}
                  onChange={e => setName(e.target.value)}
                  placeholder="Presupuesto Anual 2026" />
              </div>
              <div className="form-group" style={{ marginTop: 12 }}>
                <label className="erp-label">EJERCICIO FISCAL *</label>
                <input className="erp-input" type="number" required min="2000" max="2100"
                  value={year} onChange={e => setYear(parseInt(e.target.value))} />
              </div>
              <div style={{ background: '#f9fafb', borderRadius: 6, padding: '8px 12px', marginTop: 12, fontSize: 12, color: '#6b7280' }}>
                Período: 01/01/{year} — 31/12/{year} (ajustable desde la API)
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12, marginTop: 20, paddingTop: 16, borderTop: '1px solid #f3f4f6' }}>
                <button type="button" className="btn-secondary" onClick={() => setShowCreate(false)}>Cancelar</button>
                <button type="submit" className="btn-primary" disabled={saving}>
                  {saving ? 'Creando...' : 'Crear Presupuesto'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ── Modal Añadir Línea ───────────────────────────────────────────────── */}
      {showAddLine && (
        <div className="modal-overlay" onClick={() => setShowAddLine(false)}>
          <div className="modal" style={{ width: 420 }} onClick={e => e.stopPropagation()}>
            <div className="modal-header">
              <h2>Añadir Línea</h2>
              <button className="modal-close" onClick={() => setShowAddLine(false)}>✕</button>
            </div>
            <form onSubmit={handleAddLine} style={{ padding: '20px 24px' }}>
              <div className="form-group">
                <label className="erp-label">TIPO *</label>
                <select className="erp-input" value={lineForm.type}
                  onChange={e => setLineForm({ ...lineForm, type: e.target.value })}>
                  <option value="Revenue">Ingreso (7xx)</option>
                  <option value="Expense">Gasto (6xx)</option>
                </select>
              </div>
              <div className="form-group" style={{ marginTop: 12 }}>
                <label className="erp-label">CÓDIGO CUENTA PGC</label>
                <input className="erp-input" value={lineForm.accountCode}
                  onChange={e => setLineForm({ ...lineForm, accountCode: e.target.value })}
                  placeholder="700, 621, 620..." />
              </div>
              <div className="form-group" style={{ marginTop: 12 }}>
                <label className="erp-label">IMPORTE PRESUPUESTADO (€) *</label>
                <input className="erp-input" type="number" step="0.01" min="0.01" required
                  value={lineForm.budgetedAmount}
                  onChange={e => setLineForm({ ...lineForm, budgetedAmount: e.target.value })}
                  placeholder="50000.00" />
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12, marginTop: 20, paddingTop: 16, borderTop: '1px solid #f3f4f6' }}>
                <button type="button" className="btn-secondary" onClick={() => setShowAddLine(false)}>Cancelar</button>
                <button type="submit" className="btn-primary" disabled={saving}>
                  {saving ? 'Añadiendo...' : 'Añadir Línea'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </PageContainer>
  );
}
