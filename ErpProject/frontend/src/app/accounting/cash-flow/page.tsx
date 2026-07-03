"use client";

import { useState } from "react";
import PageContainer from "@/components/PageContainer";

interface CashFlow {
  operatingActivitiesCash: number;
  investingActivitiesCash: number;
  financingActivitiesCash: number;
  netChangeInCash: number;
  beginningCash: number;
  endingCash: number;
  status: string;
}

export default function CashFlowPage() {
  const [year, setYear] = useState(new Date().getFullYear());
  const [cashFlow, setCashFlow] = useState<CashFlow | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const eur = (n: number) => n.toLocaleString("es-ES", { style: "currency", currency: "EUR" });

  const generateCashFlow = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch("/api/proxy/v1/accounting/financial-statements/cash-flow", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ fiscalYear: year }),
      });
      if (!res.ok) {
        const e = await res.json().catch(() => ({}));
        throw new Error(e.error || "Error al generar el estado de flujos");
      }
      setCashFlow(await res.json());
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error de conexiÃ³n");
    } finally {
      setLoading(false);
    }
  };

  return (
    <PageContainer>
      <h1 className="page-title mb-4">Estado de flujos de efectivo (EFE)</h1>
      {error && <div className="mb-4 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</div>}

      <div className="erp-card mb-4 flex flex-wrap items-end gap-3">
        <div>
          <label className="erp-label">Ejercicio</label>
          <input className="erp-input w-28" type="number" value={year} onChange={(e) => setYear(Number(e.target.value))} />
        </div>
        <button className="btn-primary" disabled={loading} onClick={generateCashFlow}>
          {loading ? "Calculandoâ€¦" : "Generar desde asientos"}
        </button>
      </div>

      {cashFlow && (
        <div className="erp-card space-y-2 text-sm">
          <p><strong>Actividades de explotaciÃ³n:</strong> {eur(cashFlow.operatingActivitiesCash)}</p>
          <p><strong>Actividades de inversiÃ³n:</strong> {eur(cashFlow.investingActivitiesCash)}</p>
          <p><strong>Actividades de financiaciÃ³n:</strong> {eur(cashFlow.financingActivitiesCash)}</p>
          <p><strong>VariaciÃ³n neta de efectivo:</strong> {eur(cashFlow.netChangeInCash)}</p>
          <p><strong>Efectivo inicial:</strong> {eur(cashFlow.beginningCash)}</p>
          <p className="font-bold"><strong>Efectivo final:</strong> {eur(cashFlow.endingCash)}</p>
          <p className="text-gray-500">{cashFlow.status}</p>
        </div>
      )}
    </PageContainer>
  );
}
