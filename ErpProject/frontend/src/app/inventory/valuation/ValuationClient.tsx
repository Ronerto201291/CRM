"use client";
import React, { useState } from "react";
import PageContainer from "@/components/PageContainer";

interface ValuationProduct {
  id: string;
  name: string;
  quantity: number;
  unitCost: number;
  totalValue: number;
}

interface ValuationClientProps {
  initialProducts: ValuationProduct[];
  initialMethod?: string;
}

const fmt = (n: number) =>
  `€ ${(n || 0).toLocaleString("es-ES", { minimumFractionDigits: 2 })}`;

export default function ValuationClient({
  initialProducts,
  initialMethod = "PMP",
}: ValuationClientProps) {
  const [method, setMethod] = useState(initialMethod);
  const [products, setProducts] = useState<ValuationProduct[]>(initialProducts);
  const [loading, setLoading] = useState(false);

  const fetchValuation = async () => {
    setLoading(true);
    try {
      const res = await fetch(`/api/proxy/v1/inventory/valuation?method=${method}`);
      if (res.ok) {
        const data = await res.json();
        setProducts(Array.isArray(data) ? data : (data.items ?? []));
      }
    } finally {
      setLoading(false);
    }
  };

  const total = products.reduce((sum, p) => sum + (p.totalValue || 0), 0);

  return (
    <PageContainer>
      <div className="page-header">
        <div>
          <h1 className="page-title">Valoración de inventario</h1>
          <p className="page-subtitle">Coste de stock por método contable</p>
        </div>
      </div>

      <div className="card mb-4">
        <div className="flex flex-wrap items-center gap-3">
          <label htmlFor="valuation-method" className="font-medium">
            Método:
          </label>
          <select
            id="valuation-method"
            value={method}
            onChange={(e) => setMethod(e.target.value)}
            className="form-select"
          >
            <option value="PMP">PMP (precio medio ponderado)</option>
            <option value="FIFO">FIFO</option>
          </select>
          <button
            type="button"
            onClick={fetchValuation}
            disabled={loading}
            className="btn btn-primary"
          >
            {loading ? "Calculando…" : "Recalcular"}
          </button>
        </div>
      </div>

      <div className="table-container">
        <table className="data-table">
          <thead>
            <tr>
              <th>Producto</th>
              <th>Cantidad</th>
              <th>Coste unitario</th>
              <th>Valor total</th>
            </tr>
          </thead>
          <tbody>
            {products.length === 0 ? (
              <tr>
                <td colSpan={4} className="text-center text-muted">
                  Sin datos de valoración
                </td>
              </tr>
            ) : (
              products.map((p) => (
                <tr key={p.id}>
                  <td>{p.name}</td>
                  <td>{p.quantity}</td>
                  <td>{fmt(p.unitCost)}</td>
                  <td>{fmt(p.totalValue)}</td>
                </tr>
              ))
            )}
          </tbody>
          {products.length > 0 && (
            <tfoot>
              <tr>
                <td colSpan={3} className="font-semibold text-right">
                  Total inventario
                </td>
                <td className="font-semibold">{fmt(total)}</td>
              </tr>
            </tfoot>
          )}
        </table>
      </div>
    </PageContainer>
  );
}
