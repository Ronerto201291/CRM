"use client";

import { useCallback, useEffect, useState } from "react";
import PageContainer from "@/components/PageContainer";

interface IspRow {
  id: string;
  supplierCountryCode: string;
  vatableBase: number;
  vatRate: number;
  vatAmount: number;
  isReverseCharge: boolean;
}

export default function ISPPage() {
  const [countryCode, setCountryCode] = useState("DE");
  const [base, setBase] = useState(1000);
  const [vatRate, setVatRate] = useState(21);
  const [ispList, setIspList] = useState<IspRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const fetchList = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch("/api/proxy/v1/accounting/isp");
      if (!res.ok) throw new Error("Error al cargar operaciones ISP");
      setIspList(await res.json());
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error de conexión");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchList(); }, [fetchList]);

  const create = async () => {
    setError(null);
    try {
      const res = await fetch("/api/proxy/v1/accounting/isp", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ supplierCountryCode: countryCode, vatableBase: base, vatRate }),
      });
      if (!res.ok) {
        const e = await res.json().catch(() => ({}));
        throw new Error(e.error || "Error al crear ISP");
      }
      await fetchList();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error al crear");
    }
  };

  const vatAmount = base * (vatRate / 100);
  const eur = (n: number) => n.toLocaleString("es-ES", { style: "currency", currency: "EUR" });

  return (
    <PageContainer>
      <h1 className="page-title mb-4">Inversión del sujeto pasivo (ISP)</h1>
      {error && <div className="mb-4 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</div>}

      <div className="erp-card mb-4">
        <div className="mb-2">
          <label className="erp-label">País proveedor</label>
          <select className="erp-input ml-2" value={countryCode} onChange={(e) => setCountryCode(e.target.value)}>
            {["DE", "FR", "IT", "BE", "NL", "PT"].map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
        </div>
        <div className="mb-2">
          <label className="erp-label">Base imponible</label>
          <input className="erp-input ml-2 w-32" type="number" value={base} onChange={(e) => setBase(Number(e.target.value))} />
        </div>
        <div className="mb-2">
          <label className="erp-label">Tipo IVA (%)</label>
          <input className="erp-input ml-2 w-24" type="number" value={vatRate} onChange={(e) => setVatRate(Number(e.target.value))} />
        </div>
        <p className="mb-3 rounded bg-blue-50 p-2 text-sm">
          <strong>IVA (autoliquidación):</strong> {eur(vatAmount)}
        </p>
        <button className="btn-primary" onClick={create}>Registrar ISP</button>
      </div>

      {loading ? (
        <p className="text-sm text-gray-500">Cargando…</p>
      ) : (
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-gray-100">
              <th className="border p-2">País</th>
              <th className="border p-2">Base</th>
              <th className="border p-2">IVA %</th>
              <th className="border p-2">Cuota IVA</th>
              <th className="border p-2">ISP</th>
            </tr>
          </thead>
          <tbody>
            {ispList.map((i) => (
              <tr key={i.id}>
                <td className="border p-2">{i.supplierCountryCode}</td>
                <td className="border p-2">{eur(i.vatableBase)}</td>
                <td className="border p-2">{i.vatRate}%</td>
                <td className="border p-2">{eur(i.vatAmount)}</td>
                <td className="border p-2">{i.isReverseCharge ? "Sí" : "No"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </PageContainer>
  );
}
