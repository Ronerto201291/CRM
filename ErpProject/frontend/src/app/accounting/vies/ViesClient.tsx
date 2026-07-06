"use client";
import React, { useState } from "react";
import { viesValidateSchema } from '@/lib/schemas/viesValidateSchema';

interface ViesValidation {
  validationId?: string;
  isValid: boolean;
  countryCode: string;
  vatNumber: string;
  name?: string;
  address?: string;
  validationStatus: string;
  advice?: string;
  errorMessage?: string;
}

export default function ViesClient() {
  const [countryCode, setCountryCode] = useState("DE");
  const [vatNumber, setVatNumber] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [validation, setValidation] = useState<ViesValidation | null>(null);

  const validate = async () => {
    const parsed = viesValidateSchema.safeParse({ countryCode, vatNumber });
    if (!parsed.success) {
      setError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');
      return;
    }

    setLoading(true);
    setError(null);
    setValidation(null);

    try {
      const res = await fetch("/api/proxy/v1/accounting/vies/validate", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ countryCode, vatNumber: vatNumber.trim() }),
      });
      const data = await res.json();
      if (!res.ok) {
        setError(data.error ?? `Error ${res.status}`);
        return;
      }
      setValidation(data);
    } catch {
      setError("Error de conexión con el backend");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">VIES — Validación IVA intracomunitario</h1>

      {error && (
        <div className="mb-4 p-3 bg-red-50 text-red-700 border border-red-200 rounded">
          {error}
        </div>
      )}

      <div className="mb-4 border p-3">
        <div>
          <label>Código país (ISO-2):</label>
          <select
            value={countryCode}
            onChange={(e) => setCountryCode(e.target.value)}
            className="border p-2 ml-2"
          >
            <option value="DE">DE — Alemania</option>
            <option value="FR">FR — Francia</option>
            <option value="IT">IT — Italia</option>
            <option value="BE">BE — Bélgica</option>
            <option value="NL">NL — Países Bajos</option>
            <option value="AT">AT — Austria</option>
            <option value="PT">PT — Portugal</option>
            <option value="ES">ES — España</option>
          </select>
        </div>
        <div className="mt-2">
          <label>NIF-IVA (sin prefijo país):</label>
          <input
            value={vatNumber}
            onChange={(e) => setVatNumber(e.target.value.toUpperCase())}
            className="border p-2 ml-2"
            placeholder="p.ej. 123456789"
            onKeyDown={(e) => e.key === "Enter" && validate()}
          />
        </div>
        <button
          onClick={validate}
          disabled={loading}
          className="mt-3 px-4 py-2 bg-blue-600 text-white rounded disabled:opacity-50"
        >
          {loading ? "Consultando VIES..." : "Validar VIES"}
        </button>
      </div>

      {validation && (
        <div className={`border p-4 ${validation.isValid ? "bg-green-50" : "bg-red-50"}`}>
          <div className="font-bold mb-2">
            {validation.countryCode}{validation.vatNumber} —{" "}
            {validation.isValid ? "NIF UE VÁLIDO" : "NO VÁLIDO"}
          </div>
          {validation.name && (
            <div><strong>Razón social:</strong> {validation.name}</div>
          )}
          {validation.address && (
            <div><strong>Dirección:</strong> {validation.address}</div>
          )}
          <div>
            <strong>Estado:</strong> {validation.validationStatus}
          </div>
          {validation.advice && (
            <p className="text-sm mt-2 italic">{validation.advice}</p>
          )}
          {validation.errorMessage && !validation.isValid && (
            <p className="text-sm mt-1 text-red-700">Detalle: {validation.errorMessage}</p>
          )}
          <p className="text-xs text-gray-500 mt-3">
            Fuente: ec.europa.eu/taxation_customs/vies
          </p>
        </div>
      )}
    </div>
  );
}
