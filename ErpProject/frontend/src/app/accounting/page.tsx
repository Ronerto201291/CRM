'use client';
import { useEffect, useState } from 'react';
import PageContainer from '@/components/PageContainer';

type Tab = 'diario' | 'balance' | 'iva' | 'liquidacion';

interface JournalEntry {
    id: string; date: string; reference: string; description: string;
    sourceType?: string; isPosted: boolean;
    lines: { accountCode: string; accountName: string; debit: number; credit: number; }[];
    totalDebit: number; totalCredit: number;
}
interface BalanceRow { accountCode: string; accountName: string; totalDebit: number; totalCredit: number; balance: number; }

export default function AccountingPage() {
    const [tab, setTab] = useState<Tab>('diario');
    const [journal, setJournal] = useState<JournalEntry[]>([]);
    const [balance, setBalance] = useState<BalanceRow[]>([]);
    const [ivaSoportado, setIvaSoportado] = useState<{ total: number; details: any[] }>({ total: 0, details: [] });
    const [ivaRepercutido, setIvaRepercutido] = useState<{ total: number; details: any[] }>({ total: 0, details: [] });
    const [liquidacion, setLiquidacion] = useState<any>(null);
    const year = new Date().getFullYear();

    useEffect(() => {
        fetch(`/api/proxy/accounting/journal?year=${year}`).then(r => r.json()).then(setJournal).catch(() => { });
        fetch(`/api/proxy/accounting/balance?year=${year}`).then(r => r.json()).then(setBalance).catch(() => { });
        fetch(`/api/proxy/accounting/iva-soportado?year=${year}`).then(r => r.json()).then(setIvaSoportado).catch(() => { });
        fetch(`/api/proxy/accounting/iva-repercutido?year=${year}`).then(r => r.json()).then(setIvaRepercutido).catch(() => { });
        fetch(`/api/proxy/accounting/liquidacion-iva?year=${year}`).then(r => r.json()).then(setLiquidacion).catch(() => { });
    }, []);

    const fmt = (n: number) => `€ ${(n || 0).toFixed(2)}`;

    const downloadCsv = async (endpoint: string, filename: string) => {
        const r = await fetch(`/api/proxy/${endpoint}`);
        if (!r.ok) return;
        const blob = await r.blob();
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = filename;
        document.body.appendChild(a); a.click();
        document.body.removeChild(a); URL.revokeObjectURL(url);
    };

    const tabs: { id: Tab; label: string; icon: string }[] = [
        { id: 'diario', label: 'Libro Diario', icon: '📒' },
        { id: 'balance', label: 'Balance', icon: '⚖️' },
        { id: 'iva', label: 'Registro IVA', icon: '📊' },
        { id: 'liquidacion', label: 'Liquidación', icon: '💰' },
    ];

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Contabilidad</h1>
                    <p className="page-subtitle">Doble partida · Plan General Contable español · Ejercicio {year}</p>
                </div>
            </div>

            {/* Summary stats */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '14px', marginBottom: '24px' }}>
                <div className="erp-card" style={{ padding: '16px 20px' }}>
                    <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '6px' }}>Asientos contabilizados</div>
                    <div style={{ fontSize: '28px', fontWeight: 800, color: 'var(--brand-primary)' }}>{journal.length}</div>
                </div>
                <div className="erp-card" style={{ padding: '16px 20px' }}>
                    <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '6px' }}>IVA Repercutido (477)</div>
                    <div style={{ fontSize: '22px', fontWeight: 800, color: 'var(--success)' }}>{fmt(ivaRepercutido.total)}</div>
                </div>
                <div className="erp-card" style={{ padding: '16px 20px' }}>
                    <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '6px' }}>IVA Soportado (472)</div>
                    <div style={{ fontSize: '22px', fontWeight: 800, color: 'var(--danger)' }}>{fmt(ivaSoportado.total)}</div>
                </div>
            </div>

            {/* Tabs */}
            <div style={{ display: 'flex', gap: '6px', marginBottom: '20px' }}>
                {tabs.map(t => (
                    <button key={t.id} onClick={() => setTab(t.id)} style={{
                        padding: '8px 16px', borderRadius: '8px', border: '1px solid var(--border)',
                        fontSize: '13px', fontWeight: tab === t.id ? 700 : 500,
                        background: tab === t.id ? 'var(--brand-primary)' : 'var(--surface)',
                        color: tab === t.id ? 'white' : 'var(--text-secondary)',
                        cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '6px',
                    }}>
                        <span>{t.icon}</span>{t.label}
                    </button>
                ))}
            </div>

            {/* LIBRO DIARIO */}
            {tab === 'diario' && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                    <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                        <button className="btn btn-secondary btn-sm" onClick={() => downloadCsv(`accounting/export/libro-diario?year=${year}`, `LibroDiario_${year}.csv`)}>
                            ⬇ Exportar CSV
                        </button>
                    </div>
                    {journal.length === 0 && <div className="erp-card"><div className="empty-state"><div className="empty-state-icon">📒</div><div className="empty-state-title">Sin asientos</div><div className="empty-state-sub">Los asientos se generan automáticamente al bloquear facturas o aprobar gastos</div></div></div>}
                    {journal.map(j => (
                        <div key={j.id} className="erp-card" style={{ padding: '18px 20px' }}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                                    <span style={{ fontWeight: 700, fontSize: '14px', fontFamily: 'monospace' }}>{j.reference}</span>
                                    <span style={{ color: 'var(--text-muted)', fontSize: '13px' }}>{new Date(j.date).toLocaleDateString('es-ES')}</span>
                                    {j.sourceType && <span style={{ fontSize: '11px', background: 'var(--info-bg)', color: 'var(--info)', padding: '2px 8px', borderRadius: '99px', fontWeight: 600 }}>{j.sourceType}</span>}
                                </div>
                                {j.isPosted && <span style={{ fontSize: '11px', background: 'var(--success-bg)', color: '#065f46', padding: '2px 10px', borderRadius: '99px', fontWeight: 700 }}>✓ Posteado</span>}
                            </div>
                            <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '12px' }}>{j.description}</p>
                            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                                <thead>
                                    <tr style={{ borderBottom: '1px solid var(--border)' }}>
                                        <th style={{ padding: '6px 8px', textAlign: 'left', color: 'var(--text-muted)', fontWeight: 600, fontSize: '11px' }}>CTA.</th>
                                        <th style={{ padding: '6px 8px', textAlign: 'left', color: 'var(--text-muted)', fontWeight: 600, fontSize: '11px' }}>NOMBRE</th>
                                        <th style={{ padding: '6px 8px', textAlign: 'right', color: 'var(--text-muted)', fontWeight: 600, fontSize: '11px' }}>DEBE</th>
                                        <th style={{ padding: '6px 8px', textAlign: 'right', color: 'var(--text-muted)', fontWeight: 600, fontSize: '11px' }}>HABER</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {j.lines?.map((l, i) => (
                                        <tr key={i} style={{ borderBottom: '1px solid var(--surface-2)' }}>
                                            <td style={{ padding: '7px 8px', fontFamily: 'monospace', fontWeight: 700, color: 'var(--brand-primary)' }}>{l.accountCode}</td>
                                            <td style={{ padding: '7px 8px' }}>{l.accountName}</td>
                                            <td style={{ padding: '7px 8px', textAlign: 'right', fontWeight: l.debit > 0 ? 700 : 400, color: l.debit > 0 ? 'var(--text-primary)' : 'var(--text-muted)' }}>
                                                {l.debit > 0 ? fmt(l.debit) : ''}
                                            </td>
                                            <td style={{ padding: '7px 8px', textAlign: 'right', fontWeight: l.credit > 0 ? 700 : 400, color: l.credit > 0 ? 'var(--text-primary)' : 'var(--text-muted)' }}>
                                                {l.credit > 0 ? fmt(l.credit) : ''}
                                            </td>
                                        </tr>
                                    ))}
                                    <tr style={{ borderTop: '2px solid var(--border)', background: 'var(--surface-2)' }}>
                                        <td colSpan={2} style={{ padding: '7px 8px', fontWeight: 700, fontSize: '12px' }}>TOTAL</td>
                                        <td style={{ padding: '7px 8px', textAlign: 'right', fontWeight: 700, color: 'var(--brand-primary)' }}>{fmt(j.totalDebit)}</td>
                                        <td style={{ padding: '7px 8px', textAlign: 'right', fontWeight: 700, color: 'var(--brand-primary)' }}>{fmt(j.totalCredit)}</td>
                                    </tr>
                                </tbody>
                            </table>
                        </div>
                    ))}
                </div>
            )}

            {/* BALANCE */}
            {tab === 'balance' && (
                <div className="erp-card" style={{ overflow: 'hidden' }}>
                    <table className="erp-table">
                        <thead><tr>
                            <th>Cuenta</th><th>Nombre</th>
                            <th style={{ textAlign: 'right' }}>Debe</th>
                            <th style={{ textAlign: 'right' }}>Haber</th>
                            <th style={{ textAlign: 'right' }}>Saldo</th>
                        </tr></thead>
                        <tbody>
                            {balance.length === 0 && <tr><td colSpan={5}><div className="empty-state"><div className="empty-state-icon">⚖️</div><div className="empty-state-title">Sin movimientos</div></div></td></tr>}
                            {balance.map((b, i) => (
                                <tr key={i}>
                                    <td><span style={{ fontFamily: 'monospace', fontWeight: 700, color: 'var(--brand-primary)' }}>{b.accountCode}</span></td>
                                    <td>{b.accountName}</td>
                                    <td style={{ textAlign: 'right' }}>{fmt(b.totalDebit)}</td>
                                    <td style={{ textAlign: 'right' }}>{fmt(b.totalCredit)}</td>
                                    <td style={{ textAlign: 'right', fontWeight: 700, color: b.balance >= 0 ? 'var(--brand-primary)' : 'var(--danger)' }}>{fmt(b.balance)}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            {/* IVA */}
            {tab === 'iva' && (
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px' }}>
                    <div className="erp-card" style={{ overflow: 'hidden' }}>
                        <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <div><div style={{ fontWeight: 700, fontSize: '14px' }}>IVA Soportado (472)</div><div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Compras y gastos</div></div>
                            <div style={{ fontSize: '20px', fontWeight: 800, color: 'var(--danger)' }}>{fmt(ivaSoportado.total)}</div>
                        </div>
                        <div style={{ maxHeight: '300px', overflowY: 'auto' }}>
                            {ivaSoportado.details?.map((d: any, i: number) => (
                                <div key={i} style={{ padding: '10px 20px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', fontSize: '13px' }}>
                                    <span style={{ color: 'var(--text-secondary)' }}>{d.reference}</span>
                                    <span style={{ fontWeight: 600 }}>{fmt(d.debit)}</span>
                                </div>
                            ))}
                            {!ivaSoportado.details?.length && <div className="empty-state" style={{ padding: '30px' }}><div className="empty-state-title">Sin movimientos</div></div>}
                        </div>
                    </div>
                    <div className="erp-card" style={{ overflow: 'hidden' }}>
                        <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <div><div style={{ fontWeight: 700, fontSize: '14px' }}>IVA Repercutido (477)</div><div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Ventas facturadas</div></div>
                            <div style={{ fontSize: '20px', fontWeight: 800, color: 'var(--success)' }}>{fmt(ivaRepercutido.total)}</div>
                        </div>
                        <div style={{ maxHeight: '300px', overflowY: 'auto' }}>
                            {ivaRepercutido.details?.map((d: any, i: number) => (
                                <div key={i} style={{ padding: '10px 20px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', fontSize: '13px' }}>
                                    <span style={{ color: 'var(--text-secondary)' }}>{d.reference}</span>
                                    <span style={{ fontWeight: 600 }}>{fmt(d.credit)}</span>
                                </div>
                            ))}
                            {!ivaRepercutido.details?.length && <div className="empty-state" style={{ padding: '30px' }}><div className="empty-state-title">Sin movimientos</div></div>}
                        </div>
                    </div>
                </div>
            )}

            {/* LIQUIDACIÓN */}
            {tab === 'liquidacion' && liquidacion && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                    <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                        <button className="btn btn-secondary btn-sm" onClick={() => downloadCsv(`accounting/export/modelo303?year=${year}&q=${Math.ceil(new Date().getMonth() / 3)}`, `Modelo303_${year}.csv`)}>
                            ⬇ Modelo 303 (CSV)
                        </button>
                        <button className="btn btn-secondary btn-sm" onClick={() => downloadCsv(`accounting/export/modelo347?year=${year}`, `Modelo347_${year}.csv`)}>
                            ⬇ Modelo 347 (CSV)
                        </button>
                    </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px' }}>
                    <div className="erp-card" style={{ padding: '24px' }}>
                        <h3 style={{ fontWeight: 700, marginBottom: '20px', fontSize: '16px' }}>Liquidación IVA – <span style={{ color: 'var(--brand-primary)' }}>{liquidacion.trimestre}</span></h3>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', fontSize: '14px' }}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', padding: '10px 0', borderBottom: '1px solid var(--border)' }}>
                                <span>IVA Repercutido (ventas)</span>
                                <span style={{ fontWeight: 700, color: 'var(--success)' }}>{fmt(liquidacion.ivaRepercutido)}</span>
                            </div>
                            <div style={{ display: 'flex', justifyContent: 'space-between', padding: '10px 0', borderBottom: '1px solid var(--border)' }}>
                                <span>IVA Soportado (compras)</span>
                                <span style={{ fontWeight: 700, color: 'var(--danger)' }}>{fmt(liquidacion.ivaSoportado)}</span>
                            </div>
                            <div style={{ display: 'flex', justifyContent: 'space-between', padding: '14px 0', borderTop: '2px solid var(--border)', marginTop: '4px' }}>
                                <span style={{ fontWeight: 800, fontSize: '15px' }}>Resultado</span>
                                <span style={{ fontWeight: 800, fontSize: '18px', color: liquidacion.resultado >= 0 ? 'var(--danger)' : 'var(--success)' }}>{fmt(liquidacion.resultado)}</span>
                            </div>
                            {liquidacion.aIngresar > 0 && (
                                <div style={{ background: 'var(--danger-bg)', border: '1px solid rgba(239,68,68,0.2)', borderRadius: '8px', padding: '14px', color: '#991b1b' }}>
                                    <div style={{ fontWeight: 700 }}>⚠️ A ingresar a Hacienda</div>
                                    <div style={{ fontSize: '20px', fontWeight: 900, marginTop: '4px' }}>{fmt(liquidacion.aIngresar)}</div>
                                </div>
                            )}
                            {liquidacion.aCompensar > 0 && (
                                <div style={{ background: 'var(--success-bg)', border: '1px solid rgba(16,185,129,0.2)', borderRadius: '8px', padding: '14px', color: '#065f46' }}>
                                    <div style={{ fontWeight: 700 }}>✓ A compensar</div>
                                    <div style={{ fontSize: '20px', fontWeight: 900, marginTop: '4px' }}>{fmt(liquidacion.aCompensar)}</div>
                                </div>
                            )}
                        </div>
                    </div>
                    <div className="erp-card" style={{ padding: '24px' }}>
                        <h3 style={{ fontWeight: 700, marginBottom: '16px', fontSize: '16px' }}>Información legal</h3>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', fontSize: '13px', color: 'var(--text-secondary)' }}>
                            <div style={{ background: 'var(--info-bg)', borderRadius: '8px', padding: '12px', color: 'var(--info)' }}>
                                <strong>Modelo 303</strong> – Autoliquidación IVA trimestral. Plazo: 20 días tras fin de trimestre.
                            </div>
                            <div style={{ background: 'var(--surface-2)', borderRadius: '8px', padding: '12px' }}>
                                <strong>T1:</strong> 20 abril · <strong>T2:</strong> 20 julio · <strong>T3:</strong> 20 octubre · <strong>T4:</strong> 30 enero
                            </div>
                            <div style={{ background: 'var(--warning-bg)', borderRadius: '8px', padding: '12px', color: '#92400e' }}>
                                ⚠️ Datos orientativos. Consúltelos con su asesor fiscal antes de presentar.
                            </div>
                        </div>
                    </div>
                </div>
            </div>
            )}
            {tab === 'liquidacion' && !liquidacion && (
                <div className="erp-card"><div className="empty-state"><div className="empty-state-icon">💰</div><div className="empty-state-title">Sin datos de liquidación</div></div></div>
            )}
        </PageContainer>
    );
}
