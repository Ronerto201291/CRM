"use client";
import React, { useState } from "react";
import PageContainer from "@/components/PageContainer";

interface CashFlowStatement {
  id: string;
  operatingCashFlow: number;
  investingCashFlow: number;
  financingCashFlow: number;
  netCashFlow: number;
  period: string;
  status: string;
  note?: string | null;
}

export default function CashFlowClient() {
  const now = new Date();
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [cashFlow, setCashFlow] = useState<CashFlowStatement | null>(null);

  const generateCashFlow = async () => {
    setLoading(true);
    setError(null);
    setCashFlow(null);
    try {
      const res = await fetch(`/api/proxy/v1/accounting/financial-statements/cash-flow`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ year, month }),
      });
      const data = await res.json();
      if (!res.ok) {
        setError(data.error ?? `Error ${res.status}`);
        return;
      }
      setCashFlow(data);
    } catch {
      setError("Error de conexión con el backend");
    } finally {
      setLoading(false);
    }
  };

  const fmt = (n: number) =>
    n.toLocaleString("es-ES", { style: "currency", currency: "EUR" });

  return (
    <PageContainer>
      <div className="page-header">
        <div>
          <h1 className="page-title">Estado de flujos de efectivo</h1>
          <p className="page-subtitle">EFE mensual desde movimientos de tesorería (PGC 57*)</p>
        </div>
      </div>

      {error && (
        <div className="erp-card" style={{ padding: "12px 16px", marginBottom: 16, color: "var(--danger)", background: "var(--danger-bg)" }}>
          {error}
        </div>
      )}

      <div className="erp-card" style={{ padding: 16, marginBottom: 16, display: "flex", gap: 12, alignItems: "flex-end" }}>
        <div>
          <label style={{ display: "block", fontSize: 11, fontWeight: 600, color: "var(--text-muted)", marginBottom: 4 }}>AÑO</label>
          <input type="number" value={year} onChange={(e) => setYear(Number(e.target.value))}
            style={{ padding: "7px 10px", borderRadius: 6, border: "1px solid var(--border)", width: 100 }} />
        </div>
        <div>
          <label style={{ display: "block", fontSize: 11, fontWeight: 600, color: "var(--text-muted)", marginBottom: 4 }}>MES</label>
          <select value={month} onChange={(e) => setMonth(Number(e.target.value))}
            style={{ padding: "7px 10px", borderRadius: 6, border: "1px solid var(--border)" }}>
            {Array.from({ length: 12 }, (_, i) => (
              <option key={i + 1} value={i + 1}>{i + 1}</option>
            ))}
          </select>
        </div>
        <button className="btn btn-primary" onClick={generateCashFlow} disabled={loading}>
          {loading ? "Calculando…" : "Generar"}
        </button>
      </div>

      {cashFlow && (
        <div className="erp-card" style={{ padding: 20 }}>
          <div style={{ fontSize: 12, color: "var(--text-muted)", marginBottom: 12 }}>
            Período {cashFlow.period} · {cashFlow.status}
          </div>
          <div style={{ display: "grid", gap: 8, fontSize: 14 }}>
            <div><strong>Actividades de explotación:</strong> {fmt(cashFlow.operatingCashFlow)}</div>
            <div><strong>Actividades de inversión:</strong> {fmt(cashFlow.investingCashFlow)}</div>
            <div><strong>Actividades de financiación:</strong> {fmt(cashFlow.financingCashFlow)}</div>
            <div style={{ fontWeight: 700, marginTop: 8 }}><strong>Variación neta de efectivo:</strong> {fmt(cashFlow.netCashFlow)}</div>
          </div>
          {cashFlow.note && (
            <p style={{ marginTop: 12, fontSize: 12, color: "var(--text-muted)" }}>{cashFlow.note}</p>
          )}
        </div>
      )}
    </PageContainer>
  );
}
