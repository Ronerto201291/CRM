"use client";
import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { parseListResponse } from "@/lib/parseListResponse";

interface InvoiceRow {
  id: string;
  clientName?: string;
  subtotal?: number;
  taxAmount?: number;
  total?: number;
  invoiceLines?: { tipoOperacion?: string }[];
}

interface ExpenseDoc {
  id: string;
  supplierName?: string;
  taxBase?: number;
  vatAmount?: number;
  total?: number;
}

interface RegistersSummary {
  purchaseTotal: number;
  salesTotal: number;
  purchaseRecords: number;
  salesRecords: number;
  intraEU: number;
}

const fmt = (n: number) =>
  `€${n.toLocaleString("es-ES", { minimumFractionDigits: 0, maximumFractionDigits: 0 })}`;

export default function IvaManagementPage() {
  const [year, setYear] = useState(new Date().getFullYear());
  const [registers, setRegisters] = useState<RegistersSummary>({
    purchaseTotal: 0,
    salesTotal: 0,
    purchaseRecords: 0,
    salesRecords: 0,
    intraEU: 0,
  });
  const [purchaseRows, setPurchaseRows] = useState<ExpenseDoc[]>([]);
  const [salesRows, setSalesRows] = useState<InvoiceRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [exporting, setExporting] = useState<string | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [invRes, expRes] = await Promise.all([
        fetch("/api/proxy/invoices?pageSize=500"),
        fetch("/api/proxy/expenses/documents"),
      ]);

      let invoices: InvoiceRow[] = [];
      if (invRes.ok) {
        const data = await invRes.json();
        invoices = parseListResponse<InvoiceRow>(data);
      }

      let expenses: ExpenseDoc[] = [];
      if (expRes.ok) {
        const data = await expRes.json();
        expenses = Array.isArray(data) ? data : parseListResponse<ExpenseDoc>(data);
      }

      const yearInvoices = invoices.filter((i) => {
        const d = (i as { issueDate?: string }).issueDate;
        return d ? new Date(d).getFullYear() === year : false;
      });
      const yearExpenses = expenses.filter((e) => {
        const d = (e as { issueDate?: string; documentDate?: string }).issueDate
          ?? (e as { documentDate?: string }).documentDate;
        return d ? new Date(d).getFullYear() === year : false;
      });

      const intraEU = yearInvoices.filter((i) =>
        (i.invoiceLines ?? []).some((l) => l.tipoOperacion === "IntraComunitario")
      ).length;

      setRegisters({
        purchaseTotal: yearExpenses.reduce((s, e) => s + (e.taxBase ?? e.total ?? 0), 0),
        salesTotal: yearInvoices.reduce((s, i) => s + (i.subtotal ?? i.total ?? 0), 0),
        purchaseRecords: yearExpenses.length,
        salesRecords: yearInvoices.length,
        intraEU,
      });
      setPurchaseRows(yearExpenses.slice(0, 5));
      setSalesRows(yearInvoices.slice(0, 5));
    } finally {
      setLoading(false);
    }
  }, [year]);

  useEffect(() => {
    load();
  }, [load]);

  const downloadExport = async (kind: "emitidas" | "recibidas") => {
    setExporting(kind);
    setMessage(null);
    try {
      const path =
        kind === "emitidas"
          ? `/api/proxy/accounting/export/libro-iva-emitidas?year=${year}`
          : `/api/proxy/accounting/export/libro-iva-recibidas?year=${year}`;
      const res = await fetch(path);
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        setMessage({
          text: (err as { error?: string }).error ?? "Error al exportar libro IVA",
          ok: false,
        });
        return;
      }
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `LibroIVA_${kind === "emitidas" ? "Emitidas" : "Recibidas"}_${year}.csv`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
      setMessage({ text: `Libro IVA ${kind} descargado correctamente.`, ok: true });
    } finally {
      setExporting(null);
    }
  };

  const sendSii = () => {
    window.location.href = `/sii?year=${year}`;
  };

  return (
    <div className="p-6">
      <div className="flex flex-wrap items-center justify-between gap-4 mb-6">
        <h1 className="text-3xl font-bold">Libros IVA Exportables (RIVA + SII)</h1>
        <div className="flex items-center gap-2">
          <label className="text-sm text-gray-600">Ejercicio</label>
          <select
            value={year}
            onChange={(e) => setYear(Number(e.target.value))}
            className="border rounded px-2 py-1"
          >
            {[year - 1, year, year + 1].map((y) => (
              <option key={y} value={y}>{y}</option>
            ))}
          </select>
        </div>
      </div>

      {message && (
        <div className={`mb-4 p-3 rounded ${message.ok ? "bg-green-50 text-green-800" : "bg-red-50 text-red-800"}`}>
          {message.text}
        </div>
      )}

      {loading ? (
        <p className="text-gray-500">Cargando datos del ejercicio {year}…</p>
      ) : (
        <>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
            <div className="border rounded-lg p-4 bg-blue-50">
              <p className="text-gray-600 text-sm">Registro Compras</p>
              <p className="text-2xl font-bold">{fmt(registers.purchaseTotal)}</p>
              <p className="text-xs text-gray-500">{registers.purchaseRecords} registros</p>
            </div>
            <div className="border rounded-lg p-4 bg-green-50">
              <p className="text-gray-600 text-sm">Registro Ventas</p>
              <p className="text-2xl font-bold">{fmt(registers.salesTotal)}</p>
              <p className="text-xs text-gray-500">{registers.salesRecords} registros</p>
            </div>
            <div className="border rounded-lg p-4 bg-purple-50">
              <p className="text-gray-600 text-sm">Operaciones Intra-UE</p>
              <p className="text-2xl font-bold">{registers.intraEU}</p>
              <p className="text-xs text-gray-500">Facturas con línea intracomunitaria</p>
            </div>
          </div>

          <div className="border rounded-lg p-4 mb-6">
            <h2 className="text-xl font-semibold mb-4">Exportar Libros Registro</h2>
            <div className="space-y-3">
              <div className="flex items-center justify-between flex-wrap gap-2">
                <div>
                  <h3 className="font-bold">RIVA — Libro IVA Emitidas (Art. 63)</h3>
                  <p className="text-sm text-gray-600">CSV desde facturas bloqueadas del ejercicio</p>
                </div>
                <button
                  type="button"
                  disabled={exporting === "emitidas"}
                  onClick={() => downloadExport("emitidas")}
                  className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700 disabled:opacity-50"
                >
                  {exporting === "emitidas" ? "Exportando…" : "Descargar CSV emitidas"}
                </button>
              </div>
              <hr />
              <div className="flex items-center justify-between flex-wrap gap-2">
                <div>
                  <h3 className="font-bold">RIVA — Libro IVA Recibidas (Art. 64)</h3>
                  <p className="text-sm text-gray-600">CSV desde gastos aprobados del ejercicio</p>
                </div>
                <button
                  type="button"
                  disabled={exporting === "recibidas"}
                  onClick={() => downloadExport("recibidas")}
                  className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700 disabled:opacity-50"
                >
                  {exporting === "recibidas" ? "Exportando…" : "Descargar CSV recibidas"}
                </button>
              </div>
              <hr />
              <div className="flex items-center justify-between flex-wrap gap-2">
                <div>
                  <h3 className="font-bold">SII (Sistema Inmediato de Información)</h3>
                  <p className="text-sm text-gray-600">Generación y envío XML vía módulo SII</p>
                </div>
                <button
                  type="button"
                  onClick={sendSii}
                  className="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700"
                >
                  Ir a envío SII
                </button>
              </div>
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div className="border rounded-lg p-4">
              <h3 className="font-bold mb-3 text-lg">Libro Registro Compras (muestra)</h3>
              <table className="w-full text-xs">
                <thead>
                  <tr className="bg-gray-100">
                    <th className="border p-1 text-left">Proveedor</th>
                    <th className="border p-1 text-right">Base Imponible</th>
                    <th className="border p-1 text-right">IVA</th>
                  </tr>
                </thead>
                <tbody>
                  {purchaseRows.length === 0 ? (
                    <tr><td colSpan={3} className="border p-2 text-gray-500">Sin gastos en {year}</td></tr>
                  ) : (
                    purchaseRows.map((row) => (
                      <tr key={row.id}>
                        <td className="border p-1">{row.supplierName ?? "—"}</td>
                        <td className="border p-1 text-right">{fmt(row.taxBase ?? row.total ?? 0)}</td>
                        <td className="border p-1 text-right">{fmt(row.vatAmount ?? 0)}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>

            <div className="border rounded-lg p-4">
              <h3 className="font-bold mb-3 text-lg">Libro Registro Ventas (muestra)</h3>
              <table className="w-full text-xs">
                <thead>
                  <tr className="bg-gray-100">
                    <th className="border p-1 text-left">Cliente</th>
                    <th className="border p-1 text-right">Base Imponible</th>
                    <th className="border p-1 text-right">IVA</th>
                  </tr>
                </thead>
                <tbody>
                  {salesRows.length === 0 ? (
                    <tr><td colSpan={3} className="border p-2 text-gray-500">Sin facturas en {year}</td></tr>
                  ) : (
                    salesRows.map((row) => (
                      <tr key={row.id}>
                        <td className="border p-1">{row.clientName ?? "—"}</td>
                        <td className="border p-1 text-right">{fmt(row.subtotal ?? row.total ?? 0)}</td>
                        <td className="border p-1 text-right">{fmt(row.taxAmount ?? 0)}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>

          <p className="mt-6 text-sm text-gray-500">
            Los CSV son orientativos (cabecera fiscal no oficial). Para SII completo use{" "}
            <Link href="/sii" className="text-blue-600 underline">/sii</Link>.
          </p>
        </>
      )}
    </div>
  );
}
