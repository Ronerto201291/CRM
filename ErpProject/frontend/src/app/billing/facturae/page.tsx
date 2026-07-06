"use client";

import { useCallback, useEffect, useState } from "react";
import PageContainer from "@/components/PageContainer";

interface InvoiceRow {
  id: string;
  number: string;
  issueDate: string;
  status: string;
  isLocked: boolean;
  total: number;
  verifactuSubmittedAt?: string | null;
}

export default function FacturaEPage() {
  const [invoices, setInvoices] = useState<InvoiceRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch("/api/proxy/invoices");
      if (!res.ok) throw new Error("Error al cargar facturas");
      const data = await res.json();
      const rows = (Array.isArray(data) ? data : data.items ?? [])
        .filter((i: InvoiceRow) => i.isLocked)
        .map((i: InvoiceRow) => ({
          id: i.id,
          number: i.number,
          issueDate: i.issueDate,
          status: i.status,
          isLocked: i.isLocked,
          total: i.total,
          verifactuSubmittedAt: (i as { verifactuSubmittedAt?: string }).verifactuSubmittedAt,
        }));
      setInvoices(rows);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error de conexion");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const download = async (url: string, filename: string, key: string) => {
    setBusy(key);
    setError(null);
    try {
      const res = await fetch(url);
      if (!res.ok) throw new Error("Error al descargar el archivo");
      const blob = await res.blob();
      const a = document.createElement("a");
      a.href = URL.createObjectURL(blob);
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(a.href);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error de descarga");
    } finally {
      setBusy(null);
    }
  };

  return (
    <PageContainer>
      <h1 className="page-title mb-4">FacturaE / VERI*FACTU</h1>
      {error && <div className="mb-4 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</div>}

      {loading ? (
        <p className="text-sm text-gray-500">Cargando facturas bloqueadas...</p>
      ) : invoices.length === 0 ? (
        <p className="text-sm text-gray-500">No hay facturas bloqueadas para generar FacturaE.</p>
      ) : (
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-gray-100">
              <th className="border p-2 text-left">Factura</th>
              <th className="border p-2 text-left">Fecha</th>
              <th className="border p-2 text-right">Total</th>
              <th className="border p-2">VERI*FACTU</th>
              <th className="border p-2">Acciones</th>
            </tr>
          </thead>
          <tbody>
            {invoices.map((inv) => (
              <tr key={inv.id}>
                <td className="border p-2 font-mono">{inv.number}</td>
                <td className="border p-2">{new Date(inv.issueDate).toLocaleDateString("es-ES")}</td>
                <td className="border p-2 text-right">
                  {inv.total.toLocaleString("es-ES", { style: "currency", currency: "EUR" })}
                </td>
                <td className="border p-2">
                  {inv.verifactuSubmittedAt ? "Enviado" : "Pendiente"}
                </td>
                <td className="border p-2 space-x-2 text-xs">
                  <button
                    className="text-blue-600 hover:underline"
                    disabled={!!busy}
                    onClick={() => download(`/api/proxy/v1/billing/facturae/${inv.id}`, `${inv.number}.xml`, `xml-${inv.id}`)}
                  >
                    XML FacturaE
                  </button>
                  <button
                    className="text-green-600 hover:underline"
                    disabled={!!busy}
                    onClick={() => download(`/api/proxy/invoices/${inv.id}/pdf`, `${inv.number}.pdf`, `pdf-${inv.id}`)}
                  >
                    PDF
                  </button>
                  <a className="text-purple-600 hover:underline" href="/verifactu">VERI*FACTU</a>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </PageContainer>
  );
}
