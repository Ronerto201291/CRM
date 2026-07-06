"use client";
import React, { useState } from "react";
import { prorrataCalculateSchema } from '@/lib/schemas/prorrataCalculateSchema';

interface ProrrataResult {
  inlandRevenue: number;
  exemptRevenue: number;
  prorrataPercentage: number;
  message?: string;
}

export default function ProrrataClient() {
  const [year, setYear] = useState(new Date().getFullYear());
  const [inlandRevenue, setInlandRevenue] = useState(100000);
  const [exemptRevenue, setExemptRevenue] = useState(20000);
  const [prorrata, setProrrata] = useState<ProrrataResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const calculate = async () => {
    const payload = { fiscalYear: year, inlandRevenue, exemptRevenue, type: 'General' as const };
    const parsed = prorrataCalculateSchema.safeParse(payload);
    if (!parsed.success) {
      setError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');
      return;
    }
    setLoading(true);
    setError(null);
    setProrrata(null);

    try {
      const res = await fetch("/api/proxy/v1/accounting/prorrata/calculate", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(parsed.data),
      });
      const data = await res.json();
      if (!res.ok) {
        setError(data.error ?? `Error ${res.status}`);
        return;
      }
      setProrrata(data);
    } catch {
      setError("Error de conexión con el backend");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">Prorrata — IVA deducible</h1>

      {error && (
        <div className="mb-4 p-3 bg-red-50 text-red-700 border border-red-200 rounded">
          {error}
        </div>
      )}

      <div className="mb-4 border p-3">
        <div>
          <label>Ejercicio fiscal:</label>
          <input
            type="number"
            value={year}
            onChange={(e) => setYear(Number(e.target.value))}
            className="border p-2 w-24 ml-2"
          />
        </div>
        <div className="mt-2">
          <label>Ingresos sujetos a IVA:</label>
          <input
            type="number"
            value={inlandRevenue}
            onChange={(e) => setInlandRevenue(Number(e.target.value))}
            className="border p-2 ml-2"
          />
        </div>
        <div className="mt-2">
          <label>Ingresos exentos:</label>
          <input
            type="number"
            value={exemptRevenue}
            onChange={(e) => setExemptRevenue(Number(e.target.value))}
            className="border p-2 ml-2"
          />
        </div>
        <button
          onClick={calculate}
          disabled={loading}
          className="mt-3 px-4 py-2 bg-blue-600 text-white rounded disabled:opacity-50"
        >
          {loading ? "Calculando..." : "Calcular prorrata"}
        </button>
      </div>

      {prorrata && (
        <div className="border p-4 bg-green-50">
          <div className="mb-2">
            <strong>Ingresos sujetos:</strong> €{prorrata.inlandRevenue.toLocaleString("es-ES")}
          </div>
          <div className="mb-2">
            <strong>Ingresos exentos:</strong> €{prorrata.exemptRevenue.toLocaleString("es-ES")}
          </div>
          <div className="mb-2 text-lg font-bold text-blue-600">
            <strong>Prorrata:</strong> {prorrata.prorrataPercentage.toFixed(2)}%
          </div>
          {prorrata.message && (
            <p className="text-sm text-gray-600">{prorrata.message}</p>
          )}
        </div>
      )}
    </div>
  );
}
