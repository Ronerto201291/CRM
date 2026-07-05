"use client";

import { useCallback, useEffect, useState } from "react";
import PageContainer from "@/components/PageContainer";

interface AeatModel {
  id: string;
  type: string;
  year: number;
  month?: number | null;
  status: string;
  totalRecords: number;
  totalAmount: number;
  message?: string;
}

export default function AeatModelsPage() {
  const currentYear = new Date().getFullYear();
  const [year, setYear] = useState(currentYear);
  const [models, setModels] = useState<AeatModel[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadModels = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch("/api/proxy/v1/accounting/aeat/models");
      if (!res.ok) {
        const e = await res.json().catch(() => ({}));
        throw new Error(e.error || "No se pudieron cargar los modelos");
      }
      setModels(await res.json());
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error de conexión");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadModels(); }, [loadModels]);

  const createModel = async (path: string, body: object, key: string) => {
    setBusy(key);
    setError(null);
    try {
      const res = await fetch(`/api/proxy/v1/accounting/aeat/${path}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      if (!res.ok) {
        const e = await res.json().catch(() => ({}));
        throw new Error(e.error || "Error al generar el modelo");
      }
      await loadModels();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error al generar");
    } finally {
      setBusy(null);
    }
  };

  const exportTxt = async (id: string) => {
    setBusy(`export-${id}`);
    setError(null);
    try {
      const res = await fetch(`/api/proxy/v1/accounting/aeat/modelo347/${id}/export-txt`, { method: "POST" });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || "Error al exportar");
      alert(data.message || `Exportado: ${data.fileName}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error al exportar");
    } finally {
      setBusy(null);
    }
  };

  const submitModel = async (id: string) => {
    setBusy(`submit-${id}`);
    setError(null);
    try {
      const res = await fetch(`/api/proxy/v1/accounting/aeat/${id}/sign-and-submit`, { method: "POST" });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || "Error al registrar envío");
      await loadModels();
      alert(data.message || "Registrado localmente");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error al enviar");
    } finally {
      setBusy(null);
    }
  };

  const eur = (n: number) => n.toLocaleString("es-ES", { style: "currency", currency: "EUR" });

  return (
    <PageContainer>
      <div className="page-header">
        <div>
          <h1 className="page-title">Modelos AEAT</h1>
          <p className="page-subtitle">Generación y seguimiento desde facturas y gastos reales</p>
        </div>
      </div>

      {error && (
        <div className="mb-4 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</div>
      )}

      <div className="mb-6 flex flex-wrap items-end gap-3">
        <div>
          <label className="erp-label">Ejercicio</label>
          <input className="erp-input w-28" type="number" value={year} onChange={(e) => setYear(Number(e.target.value))} />
        </div>
        <button
          className="btn-primary"
          disabled={!!busy}
          onClick={() => createModel("modelo347", { year }, "347")}
        >
          {busy === "347" ? "Generando…" : "Generar Modelo 347"}
        </button>
        <button
          className="btn-secondary"
          disabled={!!busy}
          onClick={() => createModel("modelo111-190", { year, month: new Date().getMonth() + 1 }, "111")}
        >
          {busy === "111" ? "Generando…" : "Generar Modelo 111"}
        </button>
        <button
          className="btn-secondary"
          disabled={!!busy}
          onClick={() => createModel("modelo200", { year }, "200")}
        >
          {busy === "200" ? "Generando…" : "Generar Modelo 200"}
        </button>
        <button
          className="btn-secondary"
          disabled={!!busy}
          onClick={() => createModel("modelo202", { year }, "202")}
        >
          {busy === "202" ? "Generando…" : "Generar Modelo 202"}
        </button>
      </div>

      <div className="erp-card">
        <h2 className="mb-4 text-lg font-semibold">Modelos generados</h2>
        {loading ? (
          <p className="text-sm text-gray-500">Cargando…</p>
        ) : models.length === 0 ? (
          <p className="text-sm text-gray-500">No hay modelos generados todavía.</p>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-100">
                <th className="border p-2 text-left">Modelo</th>
                <th className="border p-2 text-left">Período</th>
                <th className="border p-2 text-right">Importe</th>
                <th className="border p-2">Estado</th>
                <th className="border p-2">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {models.map((m) => (
                <tr key={m.id}>
                  <td className="border p-2 font-semibold">{m.type}</td>
                  <td className="border p-2">{m.month ? `${m.month}/` : ""}{m.year}</td>
                  <td className="border p-2 text-right">{eur(m.totalAmount)}</td>
                  <td className="border p-2">{m.status}</td>
                  <td className="border p-2 space-x-2">
                    {m.type === "347" && (
                      <button className="text-blue-600 hover:underline text-xs" disabled={!!busy} onClick={() => exportTxt(m.id)}>
                        Descargar TXT
                      </button>
                    )}
                    <button className="text-green-600 hover:underline text-xs" disabled={!!busy} onClick={() => submitModel(m.id)}>
                      Registrar envío
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </PageContainer>
  );
}
