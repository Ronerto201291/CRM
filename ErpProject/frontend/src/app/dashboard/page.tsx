'use client';
import { useEffect, useState } from 'react';
import PageContainer from '@/components/PageContainer';

interface DashboardStats {
  revenue: number;
  totalExpenses: number;
  profit: number;
  ivaRepercutido: number;
  ivaSoportado: number;
  pendingExpenses: number;
  recentInvoices: any[];
  recentExpenses: any[];
}

export default function DashboardPage() {
  const [data, setData] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function loadData() {
      try {
        const [invoicesRes, expStatsRes, ivaRes] = await Promise.all([
          fetch('/api/proxy/invoices'),
          fetch('/api/proxy/expenses/stats'),
          fetch('/api/proxy/accounting/liquidacion-iva'),
        ]);
        const invoices = invoicesRes.ok ? await invoicesRes.json() : [];
        const expStats = expStatsRes.ok ? await expStatsRes.json() : { totalBase: 0, totalVATSoportado: 0, pending: 0, approved: 0 };
        const iva = ivaRes.ok ? await ivaRes.json() : { ivaRepercutido: 0, ivaSoportado: 0 };

        const revenue = invoices
          .filter((i: any) => i.status === 'Paid' || i.status === 'Locked')
          .reduce((sum: number, i: any) => sum + (i.total || 0), 0);

        setData({
          revenue,
          totalExpenses: expStats.totalBase || 0,
          profit: revenue - (expStats.totalBase || 0),
          ivaRepercutido: iva.ivaRepercutido || 0,
          ivaSoportado: iva.ivaSoportado || 0,
          pendingExpenses: expStats.pending || 0,
          recentInvoices: invoices.slice(0, 6),
          recentExpenses: [],
        });
      } catch {
        setData({ revenue: 0, totalExpenses: 0, profit: 0, ivaRepercutido: 0, ivaSoportado: 0, pendingExpenses: 0, recentInvoices: [], recentExpenses: [] });
      }
      setLoading(false);
    }
    loadData();
  }, []);

  if (loading) return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100vh', color: 'var(--text-muted)', fontFamily: 'Inter, sans-serif' }}>
      <div style={{ textAlign: 'center' }}>
        <div style={{ fontSize: '32px', marginBottom: '12px' }}>⟳</div>
        <p>Cargando dashboard...</p>
      </div>
    </div>
  );

  const d = data!;
  const fmt = (n: number) => `€ ${n.toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  const ivaBalance = d.ivaRepercutido - d.ivaSoportado;

  return (
    <PageContainer>
      {/* Header */}
      <div className="page-header">
        <div>
          <h1 className="page-title">Dashboard</h1>
          <p className="page-subtitle">
            {new Date().toLocaleDateString('es-ES', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' })}
          </p>
        </div>
      </div>

      {/* KPI Grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px', marginBottom: '24px' }}>
        <StatCard
          title="Ingresos Cobrados"
          value={fmt(d.revenue)}
          icon="💰" iconBg="#ecfdf5" iconColor="#065f46"
          sub="Facturas pagadas"
          trend="positive"
        />
        <StatCard
          title="Gastos Aprobados"
          value={fmt(d.totalExpenses)}
          icon="🧾" iconBg="#fef2f2" iconColor="#991b1b"
          sub="Base de gastos contabilizados"
          trend="negative"
        />
        <StatCard
          title="Resultado Neto"
          value={fmt(d.profit)}
          icon="📈" iconBg={d.profit >= 0 ? '#eff6ff' : '#fef2f2'} iconColor={d.profit >= 0 ? '#1e40af' : '#991b1b'}
          sub="Ingresos – Gastos"
          trend={d.profit >= 0 ? 'positive' : 'negative'}
        />
        <StatCard
          title="IVA a Liquidar"
          value={fmt(ivaBalance)}
          icon="🏛️" iconBg="#fffbeb" iconColor="#92400e"
          sub={ivaBalance >= 0 ? 'A ingresar a Hacienda' : 'A compensar'}
          trend="neutral"
        />
      </div>

      {/* Secondary KPIs */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px', marginBottom: '28px' }}>
        <SecondaryCard icon="⏳" label="Gastos pendientes de revisar" value={String(d.pendingExpenses)} unit="gastos" color="#f59e0b" />
        <SecondaryCard icon="↑" label="IVA Repercutido (Ventas)" value={fmt(d.ivaRepercutido)} unit="cuenta 477" color="#10b981" />
        <SecondaryCard icon="↓" label="IVA Soportado (Compras)" value={fmt(d.ivaSoportado)} unit="cuenta 472" color="#3b82f6" />
      </div>

      {/* Tables */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 0.7fr', gap: '20px' }}>
        {/* Recent invoices */}
        <div className="erp-card" style={{ overflow: 'hidden' }}>
          <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontWeight: 700, fontSize: '14px' }}>Últimas Facturas</span>
            <a href="/billing" style={{ fontSize: '12px', color: 'var(--brand-primary)', textDecoration: 'none', fontWeight: 500 }}>Ver todo →</a>
          </div>
          {d.recentInvoices.length === 0 ? (
            <div className="empty-state">
              <div className="empty-state-icon">🧾</div>
              <div className="empty-state-title">Sin facturas</div>
              <div className="empty-state-sub">Crea tu primera factura</div>
            </div>
          ) : (
            <table className="erp-table">
              <thead>
                <tr>
                  <th>Número</th>
                  <th>Cliente</th>
                  <th style={{ textAlign: 'right' }}>Total</th>
                  <th>Estado</th>
                </tr>
              </thead>
              <tbody>
                {d.recentInvoices.map((inv: any) => (
                  <tr key={inv.id}>
                    <td style={{ fontWeight: 600 }}>{inv.number}</td>
                    <td style={{ color: 'var(--text-secondary)' }}>{inv.clientName || '—'}</td>
                    <td style={{ textAlign: 'right', fontWeight: 600 }}>{fmt(inv.total || 0)}</td>
                    <td><StatusBadge status={inv.status} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Quick actions */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
          <div className="erp-card" style={{ padding: '20px' }}>
            <h3 style={{ fontSize: '14px', fontWeight: 700, marginBottom: '14px' }}>Acciones rápidas</h3>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
              <QuickAction href="/billing" icon="🧾" label="Nueva Factura" color="#2563eb" />
              <QuickAction href="/crm" icon="👤" label="Nuevo Cliente" color="#10b981" />
              <QuickAction href="/expenses" icon="📸" label="Revisar Gastos" color="#f59e0b" badge={d.pendingExpenses > 0 ? d.pendingExpenses : undefined} />
              <QuickAction href="/accounting" icon="📊" label="Ver Contabilidad" color="#8b5cf6" />
            </div>
          </div>

          <div className="erp-card" style={{ padding: '20px' }}>
            <h3 style={{ fontSize: '14px', fontWeight: 700, marginBottom: '12px' }}>Resumen IVA trimestral</h3>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', fontSize: '13px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span style={{ color: 'var(--text-secondary)' }}>IVA Repercutido</span>
                <span style={{ fontWeight: 600, color: 'var(--success)' }}>{fmt(d.ivaRepercutido)}</span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span style={{ color: 'var(--text-secondary)' }}>IVA Soportado</span>
                <span style={{ fontWeight: 600, color: 'var(--danger)' }}>{fmt(d.ivaSoportado)}</span>
              </div>
              <div style={{ height: '1px', background: 'var(--border)', margin: '4px 0' }} />
              <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span style={{ fontWeight: 700 }}>{ivaBalance >= 0 ? 'A ingresar' : 'A compensar'}</span>
                <span style={{ fontWeight: 800, color: ivaBalance >= 0 ? 'var(--danger)' : 'var(--success)' }}>{fmt(Math.abs(ivaBalance))}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </PageContainer>
  );
}

function StatCard({ title, value, icon, iconBg, iconColor, sub, trend }: any) {
  return (
    <div className="stat-card">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <div style={{ flex: 1 }}>
          <div className="stat-card-title">{title}</div>
          <div className="stat-card-value" style={{ marginTop: '8px', fontSize: '22px' }}>{value}</div>
          <div className="stat-card-sub" style={{ marginTop: '4px' }}>{sub}</div>
        </div>
        <div className="stat-card-icon" style={{ background: iconBg, color: iconColor }}>{icon}</div>
      </div>
    </div>
  );
}

function SecondaryCard({ icon, label, value, unit, color }: any) {
  return (
    <div className="erp-card" style={{ padding: '16px 20px', display: 'flex', alignItems: 'center', gap: '14px' }}>
      <div style={{ fontSize: '22px', width: '40px', textAlign: 'center' }}>{icon}</div>
      <div style={{ flex: 1 }}>
        <div style={{ fontSize: '11px', color: 'var(--text-muted)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em' }}>{label}</div>
        <div style={{ fontSize: '18px', fontWeight: 800, color: color, marginTop: '2px' }}>{value}</div>
        <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{unit}</div>
      </div>
    </div>
  );
}

function QuickAction({ href, icon, label, color, badge }: any) {
  return (
    <a href={href} style={{
      display: 'flex', alignItems: 'center', gap: '12px', padding: '10px 12px',
      borderRadius: '8px', border: '1px solid var(--border)', textDecoration: 'none',
      background: 'var(--surface)', transition: 'all 0.15s',
    }}
      onMouseEnter={e => { (e.currentTarget as HTMLElement).style.background = 'var(--surface-2)'; (e.currentTarget as HTMLElement).style.borderColor = color; }}
      onMouseLeave={e => { (e.currentTarget as HTMLElement).style.background = 'var(--surface)'; (e.currentTarget as HTMLElement).style.borderColor = 'var(--border)'; }}
    >
      <span style={{ fontSize: '18px' }}>{icon}</span>
      <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-primary)', flex: 1 }}>{label}</span>
      {badge && <span style={{ background: color, color: 'white', borderRadius: '99px', padding: '1px 8px', fontSize: '11px', fontWeight: 700 }}>{badge}</span>}
      <span style={{ color: 'var(--text-muted)', fontSize: '16px' }}>›</span>
    </a>
  );
}

const statusMap: Record<string, { label: string; cls: string }> = {
  Draft: { label: 'Borrador', cls: 'badge-gray' },
  Issued: { label: 'Emitida', cls: 'badge-info' },
  Paid: { label: 'Pagada', cls: 'badge-success' },
  Locked: { label: 'Bloqueada', cls: 'badge-danger' },
};
function StatusBadge({ status }: { status: string }) {
  const s = statusMap[status] ?? { label: status, cls: 'badge-gray' };
  return <span className={`badge ${s.cls}`}>{s.label}</span>;
}
