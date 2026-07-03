"use client";

import { useState } from "react";
import PageContainer from "@/components/PageContainer";

interface AgingData {
  type: string;
  totalAmount: number;
  current: number;
  days31To60: number;
  days61To90: number;
  days91Plus: number;
  dso?: number;
  dpo?: number;
}

export default function AgingPage() {
  const [agingData, setAgingData] = useState<AgingData | null>(null);
  const [dso, setDso] = useState<number | null>(null);
  const [dpo, setDpo] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState<string | null>(null);

  const eur = (n: number) => n.toLocaleString("es-ES", { style: "currency", currency: "EUR" });

  const fetchAging = async (type: "receivables" | "payables") => {
    setLoading(type);
    setError(null);
    try {
      const res = await fetch(`/api/proxy/v1/accounting/aging/${type}`);
      if (!res.ok) throw new Error("Error al cargar antigÃ¼edad");
      setAgingData(await res.json());
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error de conexiÃ³n");
    } finally {
      setLoading(null);
    }
  };

  const fetchDSO = async () => {
    setLoading("dso");
    setError(null);
    try {
      const res = await fetch("/api/proxy/v1/accounting/aging/dso");
      if (!res.ok) throw new Error("Error al calcular DSO");
      const data = await res.json();
      setDso(data.dso);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error de conexiÃ³n");
    } finally {
      setLoading(null);
    }
  };

  const fetchDPO = async () => {
    setLoading("dpo");
    setError(null);
    try {
      const res = await fetch("/api/proxy/v1/accounting/aging/dpo");
      if (!res.ok) throw new Error("Error al calcular DPO");
      const data = await res.json();
      setDpo(data.dpo);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error de conexiÃ³n");
    } finally {
      setLoading(null);
    }
  };

  return (
    <PageContainer>
      <h1 className="page-title mb-4">AntigÃ¼edad de cobros y pagos</h1>
      {error && <div className="mb-4 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</div>}

      <div className="mb-4 flex flex-wrap gap-2">
        <button className="btn-primary" disabled={!!loading} onClick={() => fetchAging("receivables")}>
          Cobros pendientes
        </button>
        <button className="btn-secondary" disabled={!!loading} onClick={() => fetchAging("payables")}>
          Pagos pendientes
        </button>
        <button className="btn-secondary" disabled={!!loading} onClick={fetchDSO}>DSO</button>
        <button className="btn-secondary" disabled={!!loading} onClick={fetchDPO}>DPO</button>
      </div>

      {agingData && (
        <div className="erp-card">
          <p className="mb-2"><strong>Tipo:</strong> {agingData.type}</p>
          <p className="mb-2"><strong>Total:</strong> {eur(agingData.totalAmount)}</p>
          <p className="mb-2"><strong>0-30 dÃ­as:</strong> {eur(agingData.current)}</p>
          <p className="mb-2"><strong>31-60 dÃ­as:</strong> {eur(agingData.days31To60)}</p>
          <p className="mb-2"><strong>61-90 dÃ­as:</strong> {eur(agingData.days61To90)}</p>
          <p className="mb-2"><strong>91+ dÃ­as:</strong> {eur(agingData.days91Plus)}</p>
        </div>
      )}

      {dso !== null && (
        <div className="mt-4 erp-card bg-blue-50">
          <strong>DSO (dÃ­as de cobro):</strong> {dso.toFixed(2)} dÃ­as
        </div>
      )}
      {dpo !== null && (
        <div className="mt-4 erp-card bg-green-50">
          <strong>DPO (dÃ­as de pago):</strong> {dpo.toFixed(2)} dÃ­as
        </div>
      )}
    </PageContainer>
  );
}
