"use client";

import { useCallback, useEffect, useState } from "react";
import PageContainer from "@/components/PageContainer";
import LegalDisclaimer, { FISCAL_EXPORT_DISCLAIMER_TEXT } from "@/components/LegalDisclaimer";

interface Summary {
  totalRecords: number;
  purchaseVat: number;
  salesVat: number;
  purchaseRecords: number;
  salesRecords: number;
  intraEuCount: number;
}

interface Line {
  name: string;
  taxId: string;
  baseAmount: number;
  vatAmount: number;
  isIntraEu: boolean;
}

export default function IvaRegistersPage() {
  const [summary, setSummary] = useState<Summary | null>(null);
  const [purchaseLines, setPurchaseLines] = useState<Line[]>([]);
  const [salesLines, setSalesLines] = useState<Line[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [sumRes, purRes, salRes] = await Promise.all([
        fetch("/api/proxy/v1/accounting/iva/registro"),
        fetch("/api/proxy/v1/accounting/iva/registro/purchase/lines?limit=50"),
        fetch("/api/proxy/v1/accounting/iva/registro/sales/lines?limit=50"),
      ]);
      if (!sumRes.ok) throw new Error("Error al cargar resumen IVA");
      const sum = await sumRes.json();
      setSummary({
        totalRecords: sum.totalRecords,
        purchaseVat: sum.purchaseVat,
        salesVat: sum.salesVat,
        purchaseRecords: sum.purchaseRecords ?? sum.totalRecords,
        salesRecords: sum.salesRecords ?? 0,
        intraEuCount: sum.intraEU ?? 0,
      });
      if (purRes.ok) setPurchaseLines(await purRes.json());
      if (salRes.ok) setSalesLines(await salRes.json());
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error de conexión");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const exportRiva = async () => {
    setBusy("riva");
    setMessage(null);
    setError(null);
    try {
      const res = await fetch("/api/proxy/v1/accounting/iva/registro/export-riva", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({}),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || "Error al exportar RIVA");
      setMessage(`${data.fileName}: ${data.totalRecords} registros, IVA ${data.totalVat}€`);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error RIVA");
    } finally {
      setBusy(null);
    }
  };

  const sendSii = async () => {
    setBusy("sii");
    setMessage(null);
    setError(null);
    try {
      const now = new Date();
      const createRes = await fetch("/api/proxy/v1/accounting/iva/sii", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ year: now.getFullYear(), month: now.getMonth() + 1 }),
      });
      const created = await createRes.json();
      if (!createRes.ok) throw new Error(created.error || "Error al crear declaración SII");

      const submitRes = await fetch(`/api/proxy/v1/accounting/iva/sii/${created.id}/submit`, { method: "POST" });
      const submitted = await submitRes.json();
      if (!submitRes.ok) throw new Error(submitted.error || "Error al enviar SII");
      setMessage(submitted.message || "Declaración SII registrada");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error SII");
    } finally {
      setBusy(null);
    }
  };

  const eur = (n: number) => n.toLocaleString("es-ES", { style: "currency", currency: "EUR" });
  const purchaseTotalBase = purchaseLines.reduce((s, l) => s + l.baseAmount, 0);
  const purchaseTotalVat = purchaseLines.reduce((s, l) => s + l.vatAmount, 0);
  const salesTotalBase = salesLines.reduce((s, l) => s + l.baseAmount, 0);
  const salesTotalVat = salesLines.reduce((s, l) => s + l.vatAmount, 0);

  return (
    <PageContainer>
      <div className="page-header">
        <div>
          <h1 className="page-title">Libros IVA (RIVA + SII)</h1>
          <p className="page-subtitle">Datos reales de facturas emitidas y gastos aprobados</p>
        </div>
      </div>

      <LegalDisclaimer title="Aviso legal — libros IVA y SII">
        {FISCAL_EXPORT_DISCLAIMER_TEXT} Los ficheros RIVA .TXT y envíos SII son orientativos hasta validación con asesoría.
      </LegalDisclaimer>

      {error && <div className="mb-4 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</div>}
      {message && <div className="mb-4 rounded border border-green-200 bg-green-50 p-3 text-sm text-green-800">{message}</div>}

      {loading || !summary ? (
        <p className="text-sm text-gray-500">Cargando libros de IVA…</p>
      ) : (
        <>
          <div className="mb-6 grid grid-cols-1 gap-4 md:grid-cols-3">
            <div className="erp-card bg-blue-50">
              <p className="text-sm text-gray-600">Registro compras</p>
              <p className="text-2xl font-bold">{eur(summary.purchaseVat)}</p>
              <p className="text-xs text-gray-500">{summary.purchaseRecords} registros</p>
            </div>
            <div className="erp-card bg-green-50">
              <p className="text-sm text-gray-600">Registro ventas</p>
              <p className="text-2xl font-bold">{eur(summary.salesVat)}</p>
              <p className="text-xs text-gray-500">{summary.salesRecords} registros</p>
            </div>
            <div className="erp-card bg-purple-50">
              <p className="text-sm text-gray-600">Operaciones intra-UE</p>
              <p className="text-2xl font-bold">{summary.intraEuCount}</p>
            </div>
          </div>

          <div className="erp-card mb-6">
            <h2 className="mb-4 text-lg font-semibold">Exportar</h2>
            <div className="flex flex-wrap gap-3">
              <button className="btn-primary" disabled={!!busy} onClick={exportRiva}>
                {busy === "riva" ? "Exportando…" : "Descargar RIVA .TXT"}
              </button>
              <button className="btn-secondary" disabled={!!busy} onClick={sendSii}>
                {busy === "sii" ? "Procesando…" : "Crear y registrar SII"}
              </button>
            </div>
          </div>

          <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
            <div className="erp-card">
              <h3 className="mb-3 font-bold">Libro registro compras</h3>
              <table className="w-full text-xs">
                <thead>
                  <tr className="bg-gray-100">
                    <th className="border p-1 text-left">Proveedor</th>
                    <th className="border p-1 text-right">Base</th>
                    <th className="border p-1 text-right">IVA</th>
                  </tr>
                </thead>
                <tbody>
                  {purchaseLines.map((l, i) => (
                    <tr key={`${l.taxId}-${i}`}>
                      <td className="border p-1">{l.name}{l.isIntraEu ? " (UE)" : ""}</td>
                      <td className="border p-1 text-right">{eur(l.baseAmount)}</td>
                      <td className="border p-1 text-right">{eur(l.vatAmount)}</td>
                    </tr>
                  ))}
                  <tr className="bg-yellow-50 font-bold">
                    <td className="border p-1">TOTAL</td>
                    <td className="border p-1 text-right">{eur(purchaseTotalBase)}</td>
                    <td className="border p-1 text-right">{eur(purchaseTotalVat)}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div className="erp-card">
              <h3 className="mb-3 font-bold">Libro registro ventas</h3>
              <table className="w-full text-xs">
                <thead>
                  <tr className="bg-gray-100">
                    <th className="border p-1 text-left">Cliente</th>
                    <th className="border p-1 text-right">Base</th>
                    <th className="border p-1 text-right">IVA</th>
                  </tr>
                </thead>
                <tbody>
                  {salesLines.map((l, i) => (
                    <tr key={`${l.taxId}-${i}`}>
                      <td className="border p-1">{l.name}{l.isIntraEu ? " (ISP)" : ""}</td>
                      <td className="border p-1 text-right">{eur(l.baseAmount)}</td>
                      <td className="border p-1 text-right">{l.isIntraEu ? "€0" : eur(l.vatAmount)}</td>
                    </tr>
                  ))}
                  <tr className="bg-yellow-50 font-bold">
                    <td className="border p-1">TOTAL</td>
                    <td className="border p-1 text-right">{eur(salesTotalBase)}</td>
                    <td className="border p-1 text-right">{eur(salesTotalVat)}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}
    </PageContainer>
  );
}
