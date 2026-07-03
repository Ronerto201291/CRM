"use client";
import React, { useCallback, useEffect, useState } from "react";
import { parseListResponse } from "@/lib/parseListResponse";
import Link from "next/link";

interface Invoice {
  id: string;
  number: string;
  issueDate: string;
  status: string;
  isLocked: boolean;
  verifactuHuella?: string | null;
  clientName?: string;
  total: number;
}

export default function FacturaEPage() {
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch("/api/proxy/invoices?pageSize=500");
      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        setError(data.error ?? `Error ${res.status}`);
        setInvoices([]);
        return;
      }
      const data = parseListResponse<Invoice>(await res.json());
      setInvoices(data.filter((i) => i.isLocked));
    } catch {
      setError("Error de conexión con el backend");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const downloadFile = async (url: string, filename: string) => {
    setActionError(null);
    try {
      const res = await fetch(url);
      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        setActionError(data.error ?? `Error ${res.status}`);
        return;
      }
      const blob = await res.blob();
      const a = document.createElement("a");
      a.href = URL.createObjectURL(blob);
      a.download = filename;
      a.click();
      URL.revokeObjectURL(a.href);
    } catch {
      setActionError("Error al descargar el archivo");
    }
  };

  return (
    <div className="p-6">
      <h1 className="text-3xl font-bold mb-2">FacturaE / VERI*FACTU</h1>
      <p className="text-gray-600 mb-6 text-sm">
        Facturas bloqueadas listas para generar XML FacturaE 3.2.2. El envío VERI*FACTU es por período en la página dedicada.
      </p>

      {error && (
        <div className="mb-4 p-3 bg-red-50 text-red-700 border border-red-200 rounded">{error}</div>
      )}
      {actionError && (
        <div className="mb-4 p-3 bg-red-50 text-red-700 border border-red-200 rounded">{actionError}</div>
      )}

      <div className="border rounded-lg p-4 mb-6">
        <div className="flex justify-between items-center mb-4">
          <h2 className="text-xl font-semibold">Facturas bloqueadas</h2>
          <Link href="/billing" className="text-blue-600 hover:underline text-sm">
            Ir a facturación →
          </Link>
        </div>

        {loading ? (
          <p className="text-gray-500">Cargando facturas...</p>
        ) : invoices.length === 0 ? (
          <p className="text-gray-500">
            No hay facturas bloqueadas. Bloquea una factura en Billing para generar FacturaE.
          </p>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-200">
                <th className="border p-2 text-left">Factura</th>
                <th className="border p-2 text-left">Cliente</th>
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
                  <td className="border p-2">{inv.clientName ?? "—"}</td>
                  <td className="border p-2">
                    {new Date(inv.issueDate).toLocaleDateString("es-ES")}
                  </td>
                  <td className="border p-2 text-right">
                    €{inv.total.toLocaleString("es-ES", { minimumFractionDigits: 2 })}
                  </td>
                  <td className="border p-2 text-center">
                    {inv.verifactuHuella ? (
                      <span className="bg-green-100 text-green-800 px-2 py-1 rounded text-xs">
                        Huella generada
                      </span>
                    ) : (
                      <span className="bg-gray-100 text-gray-600 px-2 py-1 rounded text-xs">
                        Pendiente
                      </span>
                    )}
                  </td>
                  <td className="border p-2 text-xs space-x-2">
                    <button
                      type="button"
                      className="text-blue-600 hover:underline"
                      onClick={() =>
                        downloadFile(
                          `/api/proxy/v1/billing/facturae/${inv.id}`,
                          `FacturaE_${inv.number.replace(/\//g, "-")}.xml`
                        )
                      }
                    >
                      XML FacturaE
                    </button>
                    <button
                      type="button"
                      className="text-green-600 hover:underline"
                      onClick={() =>
                        downloadFile(
                          `/api/proxy/invoices/${inv.id}/pdf`,
                          `Factura_${inv.number.replace(/\//g, "-")}.pdf`
                        )
                      }
                    >
                      PDF
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <div className="flex gap-4">
        <Link
          href="/billing"
          className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700"
        >
          Nueva factura
        </Link>
        <Link
          href="/verifactu"
          className="bg-purple-600 text-white px-4 py-2 rounded hover:bg-purple-700"
        >
          Enviar VERI*FACTU (período)
        </Link>
      </div>
    </div>
  );
}
