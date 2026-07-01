'use client';
import { useEffect, useState, useCallback } from 'react';
import PageContainer from '@/components/PageContainer';

interface FiscalPeriod {
    id: string;
    fiscalYear: number;
    closedAt: string;
    resultadoNeto: number;
    closingJournalEntryId: string;
    notes: string;
}

export default function CierreContablePage() {
    const currentYear = new Date().getFullYear();
    const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
    const [year, setYear] = useState(currentYear - 1);
    const [loading, setLoading] = useState(false);
    const [fetching, setFetching] = useState(true);
    const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

    const load = useCallback(async () => {
        setFetching(true);
        try {
            const r = await fetch('/api/proxy/accounting/cierre');
            if (r.ok) setPeriods(await r.json());
        } finally { setFetching(false); }
    }, []);

    useEffect(() => { load(); }, [load]);

    const closedYears = new Set(periods.map(p => p.fiscalYear));
    const isCurrentClosed = closedYears.has(year);

    const handleClose = async () => {
        if (!confirm(
            `⚠️ CIERRE CONTABLE — Ejercicio ${year}\n\n` +
            `Esta acción es IRREVERSIBLE:\n` +
            `• Generará el asiento de cierre PGC (cuentas 6xx/7xx → 129)\n` +
            `• Bloqueará el período: no se podrán añadir más asientos en ${year}\n\n` +
            `¿Confirmar cierre del ejercicio ${year}?`
        )) return;

        setLoading(true);
        setMessage(null);
        try {
            const r = await fetch('/api/proxy/accounting/cierre', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ fiscalYear: year }),
            });
            const data = await r.json();
            if (r.ok) {
                setMessage({ text: (data as { message?: string }).message || 'Cierre realizado correctamente', ok: true });
                load();
            } else {
                setMessage({ text: (data as { error?: string }).error || 'Error al ejecutar el cierre', ok: false });
            }
        } finally { setLoading(false); }
    };

    const fmt = (n: number) => `${n >= 0 ? '+' : ''}${n.toLocaleString('es-ES', { minimumFractionDigits: 2 })} €`;

    return (
        <PageContainer>
            {/* Header */}
            <div className="page-header" style={{ marginBottom: '24px' }}>
                <div>
                    <h1 className="page-title">Cierre Contable</h1>
                    <p className="page-subtitle">
                        Cierre de ejercicio fiscal · PGC (RD 1514/2007) · Asientos 6xx/7xx → cta. 129
                    </p>
                </div>
            </div>

            {/* Info */}
            <div style={{
                padding: '14px 18px', borderRadius: '10px', marginBottom: '24px',
                background: 'var(--surface)', border: '1.5px solid var(--border)',
                fontSize: '12px', color: 'var(--text-secondary)', lineHeight: 1.8,
            }}>
                <strong style={{ color: 'var(--text-primary)', fontSize: '13px' }}>¿Qué hace el cierre contable?</strong><br />
                • Calcula el saldo de todas las cuentas de <strong>Ingresos (7xx)</strong> y <strong>Gastos (6xx)</strong> del ejercicio.<br />
                • Genera el <strong>asiento de cierre</strong> que lleva el resultado a la cuenta <strong>129 — Resultado del Ejercicio</strong>.<br />
                • <strong>Bloquea el período</strong>: ningún asiento nuevo podrá tener fecha en ese año.<br />
                • La operación es <strong>irreversible</strong> una vez ejecutada.
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.6fr', gap: '20px', alignItems: 'start' }}>
                {/* Execute close */}
                <div className="erp-card" style={{ padding: '24px' }}>
                    <h2 style={{ fontSize: '13px', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-muted)', marginBottom: '20px' }}>
                        Ejecutar Cierre
                    </h2>

                    <div className="form-group" style={{ marginBottom: '20px' }}>
                        <label className="erp-label">EJERCICIO FISCAL</label>
                        <select className="erp-input" value={year} onChange={e => { setYear(+e.target.value); setMessage(null); }}>
                            {[currentYear - 1, currentYear - 2, currentYear - 3, currentYear].map(y => (
                                <option key={y} value={y}>{y}{closedYears.has(y) ? ' ✓ Cerrado' : ''}</option>
                            ))}
                        </select>
                    </div>

                    {/* Status badge */}
                    <div style={{
                        padding: '10px 14px', borderRadius: '8px', marginBottom: '20px',
                        background: isCurrentClosed ? 'var(--success-bg)' : 'var(--surface-2)',
                        color: isCurrentClosed ? 'var(--success)' : 'var(--text-secondary)',
                        fontSize: '12px', fontWeight: 600,
                    }}>
                        {isCurrentClosed
                            ? `✓ Ejercicio ${year} cerrado el ${new Date(periods.find(p => p.fiscalYear === year)!.closedAt).toLocaleDateString('es-ES')}`
                            : `⚠ Ejercicio ${year} abierto — pendiente de cierre`}
                    </div>

                    {message && (
                        <div style={{
                            padding: '12px 14px', borderRadius: '8px', marginBottom: '16px',
                            background: message.ok ? 'var(--success-bg)' : 'var(--danger-bg)',
                            color: message.ok ? 'var(--success)' : 'var(--danger)',
                            fontSize: '12px', fontWeight: 500,
                        }}>{message.text}</div>
                    )}

                    <button
                        className="btn btn-primary"
                        style={{ width: '100%', justifyContent: 'center', opacity: isCurrentClosed ? 0.4 : 1 }}
                        disabled={loading || isCurrentClosed}
                        onClick={handleClose}
                    >
                        {loading ? '⏳ Cerrando ejercicio...' : `🔒 Cerrar Ejercicio ${year}`}
                    </button>

                    {isCurrentClosed && (
                        <div style={{ fontSize: '11px', color: 'var(--text-muted)', textAlign: 'center', marginTop: '8px' }}>
                            Este ejercicio ya fue cerrado y no puede volver a cerrarse.
                        </div>
                    )}
                </div>

                {/* History */}
                <div className="erp-card" style={{ padding: '0', overflow: 'hidden' }}>
                    <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)' }}>
                        <h2 style={{ fontSize: '13px', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-muted)' }}>
                            Histórico de Cierres
                        </h2>
                    </div>
                    {fetching ? (
                        <div style={{ padding: '32px', textAlign: 'center', color: 'var(--text-muted)', fontSize: '13px' }}>Cargando...</div>
                    ) : periods.length === 0 ? (
                        <div className="empty-state" style={{ padding: '40px' }}>
                            <div className="empty-state-icon">📅</div>
                            <div className="empty-state-title">Sin cierres registrados</div>
                            <div className="empty-state-sub">Aún no se ha ejecutado ningún cierre contable</div>
                        </div>
                    ) : (
                        <table className="erp-table">
                            <thead>
                                <tr>
                                    <th>Ejercicio</th>
                                    <th>Fecha Cierre</th>
                                    <th style={{ textAlign: 'right' }}>Resultado Neto</th>
                                    <th>Asiento</th>
                                </tr>
                            </thead>
                            <tbody>
                                {periods.map(p => (
                                    <tr key={p.id}>
                                        <td>
                                            <span style={{ fontWeight: 700, fontFamily: 'monospace', color: 'var(--brand-primary)' }}>
                                                {p.fiscalYear}
                                            </span>
                                            <span className="badge badge-success" style={{ marginLeft: '8px', fontSize: '10px' }}>✓ Cerrado</span>
                                        </td>
                                        <td style={{ color: 'var(--text-secondary)', fontSize: '12px' }}>
                                            {new Date(p.closedAt).toLocaleDateString('es-ES', { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })}
                                        </td>
                                        <td style={{
                                            textAlign: 'right', fontWeight: 700, fontFamily: 'monospace',
                                            color: p.resultadoNeto >= 0 ? 'var(--success)' : 'var(--danger)',
                                        }}>
                                            {fmt(p.resultadoNeto)}
                                        </td>
                                        <td style={{ fontSize: '11px', fontFamily: 'monospace', color: 'var(--text-muted)' }}>
                                            CIERRE-{p.fiscalYear}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    )}
                </div>
            </div>
        </PageContainer>
    );
}
