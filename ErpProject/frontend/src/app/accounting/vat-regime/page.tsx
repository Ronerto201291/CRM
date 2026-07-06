"use client";

import { useCallback, useEffect, useState } from "react";
import PageContainer from "@/components/PageContainer";

interface VatRegimeRow {
  id: string;
  type: string;
  isActive: boolean;
  effectiveDate: string;
}

export default function VatRegimePage() {
  const [regime, setRegime] = useState("Standard");
  const [regimes, setRegimes] = useState<VatRegimeRow[]>([]);
  const [effectiveDate, setEffectiveDate] = useState(new Date().toISOString().slice(0, 10));
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [currentRes, listRes] = await Promise.all([
        fetch("/api/proxy/v1/accounting/vat/regime"),
        fetch("/api/proxy/v1/accounting/vat/regimes"),
      ]);
      if (currentRes.ok) {
        const current = await currentRes.json();
        if (current.type) setRegime(current.type);
      }
      if (listRes.ok) setRegimes(await listRes.json());
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error de conexión");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const setRegimeClick = async () => {
    setError(null);
    try {
      const res = await fetch("/api/proxy/v1/accounting/vat/regime", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ type: regime, effectiveDate }),
      });
      if (!res.ok) {
        const e = await res.json().catch(() => ({}));
        throw new Error(e.error || "Error al guardar régimen");
      }
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error al guardar");
    }
  };

  const descriptions: Record<string, string> = {
    Standard: "Devengo: reconocimiento en fecha de factura",
    CashBasis: "Caja: reconocimiento en fecha de cobro/pago",
    Prorrata: "Prorrata: IVA deducible según proporción de ingresos",
    EquivalenceSurcharge: "Recargo de equivalencia en compras",
    InversionSubject: "Inversión del sujeto pasivo intracomunitaria",
  };

  return (
    <PageContainer>
      <h1 className="page-title mb-4">Régimen de IVA</h1>
      {error && <div className="mb-4 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</div>}

      <div className="erp-card mb-4">
        <label className="erp-label">Régimen</label>
        <select className="erp-input mr-2" value={regime} onChange={(e) => setRegime(e.target.value)}>
          {Object.keys(descriptions).map((k) => <option key={k} value={k}>{k}</option>)}
        </select>
        <input className="erp-input mr-2" type="date" value={effectiveDate} onChange={(e) => setEffectiveDate(e.target.value)} />
        <button className="btn-primary" onClick={setRegimeClick}>Activar régimen</button>
        <p className="mt-2 text-sm text-gray-600">{descriptions[regime]}</p>
      </div>

      <div className="erp-card">
        <h2 className="mb-3 font-semibold">Historial</h2>
        {loading ? (
          <p className="text-sm text-gray-500">Cargando…</p>
        ) : regimes.length === 0 ? (
          <p className="text-sm text-gray-500">Sin registros — se usará Standard por defecto.</p>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-100">
                <th className="border p-2 text-left">Régimen</th>
                <th className="border p-2 text-left">Vigente desde</th>
                <th className="border p-2">Activo</th>
              </tr>
            </thead>
            <tbody>
              {regimes.map((r) => (
                <tr key={r.id}>
                  <td className="border p-2">{r.type}</td>
                  <td className="border p-2">{new Date(r.effectiveDate).toLocaleDateString("es-ES")}</td>
                  <td className="border p-2">{r.isActive ? "Sí" : "No"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </PageContainer>
  );
}
