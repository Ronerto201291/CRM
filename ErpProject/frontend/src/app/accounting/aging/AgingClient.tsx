'use client';

import PageListLayout from '@/components/PageListLayout';

export interface AgingLine {
    id: string;
    reference: string;
    counterpartyName: string;
    referenceDate: string;
    daysOutstanding: number;
    amount: number;
    status: string;
}

export interface AgingBucket {
    type: string;
    totalAmount: number;
    current: number;
    days31To60: number;
    days61To90: number;
    days91Plus: number;
    lines: AgingLine[];
}

export interface AgingReport {
    reportDate: string;
    receivables: AgingBucket;
    payables: AgingBucket;
    dso: number;
    dpo: number;
    note: string;
}

const fmt = (n: number) =>
    n.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' });

function BucketTable({ bucket, title }: { bucket: AgingBucket; title: string }) {
    return (
        <div className="erp-card" style={{ padding: 20, marginBottom: 24 }}>
            <h3 style={{ marginBottom: 12 }}>{title}</h3>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 12, marginBottom: 16 }}>
                {[
                    ['0–30 d', bucket.current],
                    ['31–60 d', bucket.days31To60],
                    ['61–90 d', bucket.days61To90],
                    ['+90 d', bucket.days91Plus],
                    ['Total', bucket.totalAmount],
                ].map(([label, val]) => (
                    <div key={label as string}>
                        <div style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{label}</div>
                        <div style={{ fontWeight: 600 }}>{fmt(val as number)}</div>
                    </div>
                ))}
            </div>
            {bucket.lines.length > 0 ? (
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Referencia</th>
                            <th>Contraparte</th>
                            <th>Fecha</th>
                            <th>Días</th>
                            <th>Importe</th>
                            <th>Estado</th>
                        </tr>
                    </thead>
                    <tbody>
                        {bucket.lines.slice(0, 20).map((l) => (
                            <tr key={l.id}>
                                <td>{l.reference}</td>
                                <td>{l.counterpartyName}</td>
                                <td>{new Date(l.referenceDate).toLocaleDateString('es-ES')}</td>
                                <td>{l.daysOutstanding}</td>
                                <td>{fmt(l.amount)}</td>
                                <td>{l.status}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            ) : (
                <p style={{ color: 'var(--text-secondary)' }}>Sin partidas pendientes.</p>
            )}
        </div>
    );
}

export default function AgingClient({ initialReport }: { initialReport: AgingReport | null }) {
    if (!initialReport) {
        return (
            <PageListLayout title="Antigüedad de saldos" subtitle="Cobros y pagos por tramos">
                <div className="erp-card" style={{ padding: 24 }}>
                    <p>Inicia sesión para ver el informe de aging.</p>
                </div>
            </PageListLayout>
        );
    }

    const asOf = new Date(initialReport.reportDate).toLocaleDateString('es-ES');

    return (
        <PageListLayout
            title="Antigüedad de saldos"
            subtitle={`Corte ${asOf} — DSO ${initialReport.dso} d · DPO ${initialReport.dpo} d`}
        >
            <p style={{ marginBottom: 16, color: 'var(--text-secondary)', fontSize: 14 }}>
                {initialReport.note}
            </p>
            <BucketTable bucket={initialReport.receivables} title="Cobros (clientes)" />
            <BucketTable bucket={initialReport.payables} title="Pagos (proveedores)" />
        </PageListLayout>
    );
}
