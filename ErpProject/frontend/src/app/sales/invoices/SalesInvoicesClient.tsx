"use client";

import React, { useState, useCallback } from "react";
import PageContainer from "@/components/PageContainer";

interface SalesInvoice {
    id: string;
    number: string;
    billingInvoiceNumber?: string;
    invoiceDate: string;
    subTotal: number;
    taxAmount: number;
    total: number;
    lineCount: number;
    createdAt: string;
}

interface SalesInvoicesClientProps {
    initialInvoices: SalesInvoice[];
}

const fmt = (n: number) =>
    `€ ${(n || 0).toLocaleString("es-ES", { minimumFractionDigits: 2 })}`;

export default function SalesInvoicesClient({ initialInvoices }: SalesInvoicesClientProps) {
    const [invoices, setInvoices] = useState<SalesInvoice[]>(initialInvoices);
    const [loading, setLoading] = useState(false);
    const [search, setSearch] = useState("");

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const q = search ? `?search=${encodeURIComponent(search)}&pageSize=500` : "?pageSize=500";
            const res = await fetch(`/api/proxy/v1/sales/invoices${q}`);
            if (res.ok) {
                const data = await res.json();
                setInvoices(Array.isArray(data) ? data : (data.items ?? []));
            }
        } finally {
            setLoading(false);
        }
    }, [search]);

    const filtered = invoices;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Facturas de venta</h1>
                    <p className="page-subtitle">Facturas de cliente del módulo Sales</p>
                </div>
                <a href="/sales/invoices/new" className="btn btn-primary">
                    + Nueva factura
                </a>
            </div>

            <div className="mb-4 flex gap-2">
                <input
                    className="erp-input"
                    placeholder="Buscar por número…"
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    onKeyDown={(e) => e.key === "Enter" && load()}
                />
                <button type="button" className="btn btn-secondary" onClick={load} disabled={loading}>
                    {loading ? "Buscando…" : "Buscar"}
                </button>
            </div>

            <div className="erp-card" style={{ overflow: "hidden" }}>
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Número</th>
                            <th>Fecha</th>
                            <th>Factura billing</th>
                            <th>Líneas</th>
                            <th>Base</th>
                            <th>IVA</th>
                            <th>Total</th>
                        </tr>
                    </thead>
                    <tbody>
                        {filtered.length === 0 ? (
                            <tr>
                                <td colSpan={7} className="text-center text-muted">
                                    {loading ? "Cargando…" : "Sin facturas de venta"}
                                </td>
                            </tr>
                        ) : (
                            filtered.map((inv) => (
                                <tr key={inv.id}>
                                    <td className="font-medium">{inv.number}</td>
                                    <td>{new Date(inv.invoiceDate).toLocaleDateString("es-ES")}</td>
                                    <td>{inv.billingInvoiceNumber ?? "—"}</td>
                                    <td>{inv.lineCount}</td>
                                    <td>{fmt(inv.subTotal)}</td>
                                    <td>{fmt(inv.taxAmount)}</td>
                                    <td className="font-semibold">{fmt(inv.total)}</td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>
        </PageContainer>
    );
}
