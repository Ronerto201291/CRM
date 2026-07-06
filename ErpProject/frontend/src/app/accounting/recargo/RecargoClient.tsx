"use client";
import React, { useState, useCallback } from "react";
import { recargoCreateSchema } from '@/lib/schemas/accountingLegacyFormSchemas';

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

interface RecargoClientProps {
    initialRecargoList: RecargoItem[];
    initialPeriod: string;
    initialYear: number;
    initialQuarter: number;
}

export default function RecargoClient({
    initialRecargoList,
    initialPeriod,
    initialYear,
    initialQuarter,
}: RecargoClientProps) {
  const [year, setYear] = useState(initialYear);
  const [quarter, setQuarter] = useState(initialQuarter);
  const [supplierVat, setSupplierVat] = useState("");
  const [supplierIsRE, setSupplierIsRE] = useState(false);
  const [baseAmount, setBaseAmount] = useState(1000);
  const [rechargeRate, setRechargeRate] = useState(5.2);
  const [recargoList, setRecargoList] = useState<RecargoItem[]>(initialRecargoList);
  const [period, setPeriod] = useState(initialPeriod);
  const [error, setError] = useState<string | null>(null);

  const fetchList = useCallback(async () => {
    setError(null);
    const res = await fetch(`/api/proxy/v1/accounting/recargo?year=${year}&q=${quarter}`);
    if (!res.ok) {
      const data = await res.json().catch(() => ({}));
      setError(data.error ?? `Error ${res.status}`);
      setRecargoList([]);
      return;
    }
    const data = await res.json();
    setPeriod(data.period ?? "");
    setRecargoList(Array.isArray(data.recargos) ? data.recargos : []);
  }, [year, quarter]);

  const create = async () => {
    setError(null);
    const parsed = recargoCreateSchema.safeParse({
      supplierVat, supplierIsRE, baseAmount, rechargeRate,
    });
    if (!parsed.success) {
      setError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');
      return;
    }
    const res = await fetch('/api/proxy/v1/accounting/recargo', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        supplierVat,
        supplierIsRE,
        baseAmount,
        rechargeRate,
      }),
    });
    if (!res.ok) {
      const data = await res.json().catch(() => ({}));
      setError(data.error ?? `Error ${res.status}`);
      return;
    }
    await fetchList();
  };

  const rechargeAmount = baseAmount * (rechargeRate / 100);

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">Recargo de Equivalencia</h1>

      {error && (
        <div className="mb-4 p-3 bg-red-50 text-red-700 border border-red-200 rounded">
          {error}
        </div>
      )}

      <div className="mb-4 flex gap-4 items-end">
        <div>
          <label className="block text-sm font-medium">Año</label>
          <input
            type="number"
            value={year}
            onChange={(e) => setYear(Number(e.target.value))}
            className="border p-2 w-24"
          />
        </div>
        <div>
          <label className="block text-sm font-medium">Trimestre</label>
          <select
            value={quarter}
            onChange={(e) => setQuarter(Number(e.target.value))}
            className="border p-2"
          >
            {[1, 2, 3, 4].map((q) => (
              <option key={q} value={q}>T{q}</option>
            ))}
          </select>
        </div>
        {period && <span className="text-sm text-gray-600">Periodo: {period}</span>}
      </div>

      <div className="mb-4 border p-3">
        <div>
          <label>NIF proveedor:</label>
          <input
            value={supplierVat}
            onChange={(e) => setSupplierVat(e.target.value)}
            className="border p-2 ml-2"
          />
        </div>
        <div className="mt-2">
          <label>Proveedor en RE:</label>
          <input
            type="checkbox"
            checked={supplierIsRE}
            onChange={(e) => setSupplierIsRE(e.target.checked)}
            className="ml-2"
          />
        </div>
        <div className="mt-2">
          <label>Base:</label>
          <input
            type="number"
            value={baseAmount}
            onChange={(e) => setBaseAmount(Number(e.target.value))}
            className="border p-2 ml-2 w-24"
          />
        </div>
        <div className="mt-2">
          <label>Tipo recargo (%):</label>
          <input
            type="number"
            step="0.1"
            value={rechargeRate}
            onChange={(e) => setRechargeRate(Number(e.target.value))}
            className="border p-2 ml-2 w-24"
          />
        </div>
        <div className="mt-2 p-2 bg-yellow-50">
          <strong>Cuota recargo:</strong> €{rechargeAmount.toFixed(2)}
        </div>
        <button
          onClick={create}
          className="mt-3 px-4 py-2 bg-blue-600 text-white rounded"
        >
          Registrar recargo
        </button>
      </div>

      <table className="w-full border">
        <thead>
          <tr className="bg-gray-200">
            <th className="border p-2">Factura</th>
            <th className="border p-2">NIF cliente</th>
            <th className="border p-2">Cliente</th>
            <th className="border p-2">Base</th>
            <th className="border p-2">Tipo %</th>
            <th className="border p-2">Cuota</th>
            <th className="border p-2">Fecha</th>
          </tr>
        </thead>
        <tbody>
          {recargoList.length === 0 ? (
            <tr>
              <td colSpan={7} className="border p-4 text-center text-gray-500">
                No hay facturas con recargo de equivalencia en este periodo
              </td>
            </tr>
          ) : (
            recargoList.map((r) => (
              <tr key={r.invoiceId}>
                <td className="border p-2">{r.invoiceNumber}</td>
                <td className="border p-2">{r.clientTaxId}</td>
                <td className="border p-2">{r.clientName}</td>
                <td className="border p-2">€{r.baseAmount.toFixed(2)}</td>
                <td className="border p-2">{r.surchargeRate}%</td>
                <td className="border p-2">€{r.surchargeAmount.toFixed(2)}</td>
                <td className="border p-2">{new Date(r.invoiceDate).toLocaleDateString('es-ES')}</td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
