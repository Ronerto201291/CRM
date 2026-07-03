"use client";
import React, { useState } from "react";
import PageContainer from "@/components/PageContainer";
import FormLabel from "@/components/FormLabel";
import { costCenterSchema } from "@/lib/schemas/costCenterSchema";

interface CostCenter {
  id: string;
  code: string;
  name: string;
  type: string;
  totalCosts: number;
}

interface CostCentersClientProps {
  initialCenters: CostCenter[];
}

const fmt = (n: number) =>
  `€ ${(n || 0).toLocaleString("es-ES", { minimumFractionDigits: 2 })}`;

const TYPE_LABELS: Record<string, string> = {
  Department: "Departamento",
  Product: "Producto",
  Project: "Proyecto",
};

export default function CostCentersClient({ initialCenters }: CostCentersClientProps) {
  const [centers, setCenters] = useState<CostCenter[]>(initialCenters);
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [type, setType] = useState<"Department" | "Product" | "Project">("Department");
  const [formError, setFormError] = useState("");
  const [saving, setSaving] = useState(false);

  const fetchCenters = async () => {
    const res = await fetch(`/api/proxy/v1/accounting/cost-centers`);
    if (res.ok) {
      const data = await res.json();
      setCenters(Array.isArray(data) ? data : (data.items ?? []));
    }
  };

  const create = async () => {
    setFormError("");
    const parsed = costCenterSchema.safeParse({ code, name, type });
    if (!parsed.success) {
      setFormError(parsed.error.issues[0]?.message ?? "Revisa el formulario");
      return;
    }
    setSaving(true);
    try {
      const res = await fetch(`/api/proxy/v1/accounting/cost-centers`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(parsed.data),
      });
      if (res.ok) {
        setName("");
        setCode("");
        await fetchCenters();
      } else {
        const data = await res.json().catch(() => ({}));
        setFormError(data.error ?? "Error al crear centro de coste");
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <PageContainer>
      <div className="page-header">
        <div>
          <h1 className="page-title">Centros de coste</h1>
          <p className="page-subtitle">Contabilidad analítica por departamento, producto o proyecto</p>
        </div>
      </div>

      <div className="card mb-4">
        <h2 className="text-lg font-semibold mb-3">Nuevo centro de coste</h2>
        {formError && (
          <div className="mb-3 p-2 bg-red-50 text-red-700 border border-red-200 rounded text-sm">
            {formError}
          </div>
        )}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-3 items-end">
          <div>
            <FormLabel htmlFor="cc-code" required>Código</FormLabel>
            <input
              id="cc-code"
              className="erp-input w-full"
              placeholder="CC-001"
              value={code}
              onChange={(e) => setCode(e.target.value)}
            />
          </div>
          <div>
            <FormLabel htmlFor="cc-name" required>Nombre</FormLabel>
            <input
              id="cc-name"
              className="erp-input w-full"
              placeholder="Ventas"
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
          </div>
          <div>
            <FormLabel htmlFor="cc-type">Tipo</FormLabel>
            <select
              id="cc-type"
              className="erp-input w-full"
              value={type}
              onChange={(e) => setType(e.target.value as typeof type)}
            >
              <option value="Department">Departamento</option>
              <option value="Product">Producto</option>
              <option value="Project">Proyecto</option>
            </select>
          </div>
          <button
            type="button"
            className="btn btn-primary"
            onClick={create}
            disabled={saving}
          >
            {saving ? "Guardando…" : "Añadir"}
          </button>
        </div>
      </div>

      <div className="table-container">
        <table className="data-table">
          <thead>
            <tr>
              <th>Código</th>
              <th>Nombre</th>
              <th>Tipo</th>
              <th>Costes acumulados</th>
            </tr>
          </thead>
          <tbody>
            {centers.length === 0 ? (
              <tr>
                <td colSpan={4} className="text-center text-muted">
                  Sin centros de coste
                </td>
              </tr>
            ) : (
              centers.map((c) => (
                <tr key={c.id}>
                  <td className="font-mono">{c.code}</td>
                  <td>{c.name}</td>
                  <td>{TYPE_LABELS[c.type] ?? c.type}</td>
                  <td>{fmt(c.totalCosts)}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </PageContainer>
  );
}
