"use client";

import { useEffect, useState } from "react";
import PageContainer from "@/components/PageContainer";

interface RecargoItem {
  invoiceId: string;
  invoiceNumber: string;
  clientTaxId: string;
  clientName: string;
  baseAmount: number;
  surchargeRate: number;
  surchargeAmount: number;
  invoiceDate: string;
}

export default function RecargoPage() {
  const year = new Date().getFullYear();
  const quarter = Math.ceil((new Date().getMonth() + 1) / 3);
  const [recargoList, setRecargoList] = useState<RecargoItem[]>([]);
  const [period, setPeriod] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const fetchList = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`/api/proxy/v1/accounting/recargo?year=${year}&q=${quarter}`);
      if (!res.ok) throw new Error("Error al cargar recargos");
      const data = await res.json();
      setPeriod(data.period ?? `T${quarter} ${year}`);
      setRecargoList(data.recargos ?? []);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error de conexión");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { fetchList(); }, []);

  const eur = (n: number) => n.toLocaleString("es-ES", { style: "currency", currency: "EUR" });

  return (
    <PageContainer>
      <h1 className="page-title mb-2">Recargo de equivalencia</h1>
      <p className="mb-4 text-sm text-gray-600">Facturas con recargo en {period || `T${quarter} ${year}`}</p>
      {error && <div className="mb-4 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</div>}

      {loading ? (
        <p className="text-sm text-gray-500">Cargando…</p>
      ) : recargoList.length === 0 ? (
        <p className="text-sm text-gray-500">No hay facturas con recargo de equivalencia en el trimestre actual.</p>
      ) : (
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-gray-100">
              <th className="border p-2">Factura</th>
              <th className="border p-2">Cliente</th>
              <th className="border p-2 text-right">Base</th>
              <th className="border p-2 text-right">% RE</th>
              <th className="border p-2 text-right">Cuota RE</th>
            </tr>
          </thead>
          <tbody>
            {recargoList.map((r) => (
              <tr key={r.invoiceId}>
                <td className="border p-2">{r.invoiceNumber}</td>
                <td className="border p-2">{r.clientName ?? r.clientTaxId}</td>
                <td className="border p-2 text-right">{eur(r.baseAmount)}</td>
                <td className="border p-2 text-right">{r.surchargeRate}%</td>
                <td className="border p-2 text-right">{eur(r.surchargeAmount)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </PageContainer>
  );
}
