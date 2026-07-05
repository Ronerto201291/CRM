'use client';
import { useEffect, useState, useRef, useCallback } from 'react';
import PageContainer from '@/components/PageContainer';

type ReportTab = 'diario' | 'mayor' | 'balance' | 'pyg';

interface DiarioLinea {
    fecha: string;
    numero: string;
    cuenta: string;
    descripcion: string;
    debe: number;
    haber: number;
    saldo: number;
}

interface MayorCuenta {
    codigo: string;
    nombre: string;
    tipo: string;
    saldoInicial: number;
    totalDebe: number;
    totalHaber: number;
    saldoFinal: number;
}

interface BalanceLinea {
    codigo: string;
    descripcion: string;
    monto: number;
}

interface BalanceSection {
    nombre: string;
    lineas: BalanceLinea[];
    total: number;
}

interface PyGLinea {
    codigo: string;
    descripcion: string;
    monto: number;
}

interface PyGSection {
    nombre: string;
    lineas: PyGLinea[];
    subTotal: number;
}

export default function ReportsClient({
    initialDiario,
    initialDateRange,
}: {
    initialDiario: { lineas: DiarioLinea[]; totalDebe: number; totalHaber: number; totalRegistros: number } | null;
    initialDateRange: { inicio: string; fin: string };
}) {
    const [tab, setTab] = useState<ReportTab>('diario');
    const [loading, setLoading] = useState(false);
    const [dateRange, setDateRange] = useState(initialDateRange);

    // Datos de reportes
    const [diario, setDiario] = useState<{ lineas: DiarioLinea[]; totalDebe: number; totalHaber: number; totalRegistros: number } | null>(initialDiario);
    const [mayor, setMayor] = useState<{ cuentas: MayorCuenta[]; totalDebe: number; totalHaber: number; totalCuentas: number } | null>(null);
    const [balance, setBalance] = useState<{ activo: BalanceSection; pasivo: BalanceSection; patrimonio: BalanceSection; totalActivo: number; totalPasivoPatrimonio: number; estaBalanceado: boolean } | null>(null);
    const [pyg, setPyg] = useState<{ ingresos: PyGSection; gastos: PyGSection; totalIngresos: number; totalGastos: number; resultadoBruto: number; resultadoNeto: number } | null>(null);
    const [loadError, setLoadError] = useState<string | null>(null);

    const fmt = (n: number) => `€ ${(n || 0).toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

    const loadReports = useCallback(async () => {
        setLoading(true);
        setLoadError(null);
        try {
            switch (tab) {
                case 'diario': {
                    const diarioRes = await fetch(`/api/proxy/reports/diario?fechaInicio=${dateRange.inicio}&fechaFin=${dateRange.fin}`);
                    if (diarioRes.ok) setDiario(await diarioRes.json());
                    else setLoadError('No se pudo cargar el libro diario');
                    break;
                }
                case 'mayor': {
                    const mayorRes = await fetch(`/api/proxy/reports/mayor?fechaInicio=${dateRange.inicio}&fechaFin=${dateRange.fin}`);
                    if (mayorRes.ok) setMayor(await mayorRes.json());
                    else setLoadError('No se pudo cargar el mayor');
                    break;
                }
                case 'balance': {
                    const balanceRes = await fetch(`/api/proxy/reports/balance?fechaCorte=${dateRange.fin}`);
                    if (balanceRes.ok) setBalance(await balanceRes.json());
                    else setLoadError('No se pudo cargar el balance');
                    break;
                }
                case 'pyg': {
                    const pygRes = await fetch(`/api/proxy/reports/pyg?fechaInicio=${dateRange.inicio}&fechaFin=${dateRange.fin}`);
                    if (pygRes.ok) setPyg(await pygRes.json());
                    else setLoadError('No se pudo cargar la cuenta de resultados');
                    break;
                }
            }
        } catch {
            setLoadError('Error de conexión con el backend');
        }
        setLoading(false);
    }, [tab, dateRange]);

    const skipInitialDiario = useRef(!!initialDiario);

    useEffect(() => {
        if (skipInitialDiario.current && tab === 'diario') {
            skipInitialDiario.current = false;
            return;
        }
        queueMicrotask(() => { void loadReports(); });
    }, [tab, dateRange, loadReports]);

    const tabs: { id: ReportTab; label: string; icon: string }[] = [
        { id: 'diario', label: 'Libro Diario', icon: '??' },
        { id: 'mayor', label: 'Mayor', icon: '??' },
        { id: 'balance', label: 'Balance Sheet', icon: '??' },
        { id: 'pyg', label: 'Profit & Loss', icon: '??' },
    ];

    return (
        <PageContainer>
            {/* Header */}
            <div className="page-header" style={{ marginBottom: '24px' }}>
                <div>
                    <h1 className="page-title">Reportes Contables</h1>
                    <p className="page-subtitle">Reportes financieros profesionales conforme normativa española</p>
                </div>
            </div>

            {/* Filtros */}
            <div style={{ display: 'flex', gap: '12px', marginBottom: '20px', alignItems: 'flex-end' }}>
                <div>
                    <label className="erp-label">DESDE</label>
                    <input
                        className="erp-input"
                        type="date"
                        value={dateRange.inicio}
                        onChange={(e) => setDateRange({ ...dateRange, inicio: e.target.value })}
                    />
                </div>
                <div>
                    <label className="erp-label">HASTA</label>
                    <input
                        className="erp-input"
                        type="date"
                        value={dateRange.fin}
                        onChange={(e) => setDateRange({ ...dateRange, fin: e.target.value })}
                    />
                </div>
                <button
                    className="btn btn-primary"
                    onClick={loadReports}
                    disabled={loading}
                >
                    {loading ? 'Cargando...' : 'Actualizar'}
                </button>
            </div>

            {loadError && (
                <div className="erp-card" style={{ padding: '12px 16px', marginBottom: 16, color: 'var(--danger)', background: 'var(--danger-bg)' }}>
                    {loadError}
                </div>
            )}

            {/* Tabs */}
            <div style={{ display: 'flex', gap: '8px', marginBottom: '20px', borderBottom: '1px solid var(--border)', paddingBottom: '12px' }}>
                {tabs.map(t => (
                    <button
                        key={t.id}
                        onClick={() => setTab(t.id)}
                        style={{
                            padding: '8px 14px', borderRadius: '6px 6px 0 0', border: 'none',
                            background: tab === t.id ? 'var(--bg-secondary)' : 'transparent',
                            color: tab === t.id ? 'var(--text-primary)' : 'var(--text-muted)',
                            fontSize: '13px', fontWeight: 600, cursor: 'pointer',
                            transition: 'all 0.2s',
                        }}
                    >
                        {t.icon} {t.label}
                    </button>
                ))}
            </div>

            {/* Contenido */}
            {loading ? (
                <div style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
                    <div style={{ fontSize: '24px', marginBottom: '12px' }}>?</div>
                    <p>Generando reporte...</p>
                </div>
            ) : (
                <>
                    {/* LIBRO DIARIO */}
                    {tab === 'diario' && diario && (
                        <div className="erp-card" style={{ overflow: 'auto' }}>
                            <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)' }}>
                                <h3 style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-primary)' }}>
                                    Libro Diario ({diario.totalRegistros} líneas)
                                </h3>
                            </div>
                            <table className="erp-table">
                                <thead>
                                    <tr>
                                        <th>Fecha</th>
                                        <th>Asiento</th>
                                        <th>Cuenta</th>
                                        <th>Descripción</th>
                                        <th style={{ textAlign: 'right' }}>Debe</th>
                                        <th style={{ textAlign: 'right' }}>Haber</th>
                                        <th style={{ textAlign: 'right' }}>Saldo</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {diario.lineas.map((linea, i) => (
                                        <tr key={i}>
                                            <td style={{ padding: '10px' }}>{linea.fecha}</td>
                                            <td style={{ padding: '10px' }}>{linea.numero}</td>
                                            <td style={{ padding: '10px' }}>{linea.cuenta}</td>
                                            <td style={{ padding: '10px', color: 'var(--text-muted)', fontSize: '11px' }}>{linea.descripcion}</td>
                                            <td style={{ padding: '10px', textAlign: 'right' }}>{linea.debe > 0 ? fmt(linea.debe) : '-'}</td>
                                            <td style={{ padding: '10px', textAlign: 'right' }}>{linea.haber > 0 ? fmt(linea.haber) : '-'}</td>
                                            <td style={{ padding: '10px', textAlign: 'right', fontWeight: 600 }}>{fmt(linea.saldo)}</td>
                                        </tr>
                                    ))}
                                </tbody>
                                <tfoot style={{ background: 'var(--bg-secondary)', fontWeight: 700, borderTop: '2px solid var(--border)' }}>
                                    <tr>
                                        <td colSpan={4} style={{ padding: '10px', textAlign: 'right' }}>TOTALES:</td>
                                        <td style={{ padding: '10px', textAlign: 'right' }}>{fmt(diario.totalDebe)}</td>
                                        <td style={{ padding: '10px', textAlign: 'right' }}>{fmt(diario.totalHaber)}</td>
                                        <td style={{ padding: '10px', textAlign: 'right' }}>{fmt(diario.totalDebe - diario.totalHaber)}</td>
                                    </tr>
                                </tfoot>
                            </table>
                        </div>
                    )}

                    {/* MAYOR */}
                    {tab === 'mayor' && mayor && (
                        <div className="erp-card" style={{ overflow: 'auto' }}>
                            <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)' }}>
                                <h3 style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-primary)' }}>
                                    Mayor Contable ({mayor.totalCuentas} cuentas)
                                </h3>
                            </div>
                            <table className="erp-table">
                                <thead>
                                    <tr>
                                        <th>Código</th>
                                        <th>Nombre</th>
                                        <th>Tipo</th>
                                        <th style={{ textAlign: 'right' }}>Debe</th>
                                        <th style={{ textAlign: 'right' }}>Haber</th>
                                        <th style={{ textAlign: 'right' }}>Saldo</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {mayor.cuentas.map((cuenta, i) => (
                                        <tr key={i}>
                                            <td style={{ padding: '10px' }}>{cuenta.codigo}</td>
                                            <td style={{ padding: '10px' }}>{cuenta.nombre}</td>
                                            <td style={{ padding: '10px', fontSize: '11px' }}>{cuenta.tipo}</td>
                                            <td style={{ padding: '10px', textAlign: 'right' }}>{fmt(cuenta.totalDebe)}</td>
                                            <td style={{ padding: '10px', textAlign: 'right' }}>{fmt(cuenta.totalHaber)}</td>
                                            <td style={{ padding: '10px', textAlign: 'right', fontWeight: 600 }}>{fmt(cuenta.saldoFinal)}</td>
                                        </tr>
                                    ))}
                                </tbody>
                                <tfoot style={{ background: 'var(--bg-secondary)', fontWeight: 700, borderTop: '2px solid var(--border)' }}>
                                    <tr>
                                        <td colSpan={3} style={{ padding: '10px', textAlign: 'right' }}>TOTALES:</td>
                                        <td style={{ padding: '10px', textAlign: 'right' }}>{fmt(mayor.totalDebe)}</td>
                                        <td style={{ padding: '10px', textAlign: 'right' }}>{fmt(mayor.totalHaber)}</td>
                                        <td></td>
                                    </tr>
                                </tfoot>
                            </table>
                        </div>
                    )}

                    {/* BALANCE SHEET */}
                    {tab === 'balance' && balance && (
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px', marginBottom: '20px' }}>
                            {/* Activo */}
                            <div className="erp-card">
                                <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)' }}>
                                    <h3 style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-primary)' }}>
                                        {balance.activo.nombre}
                                    </h3>
                                </div>
                                <div style={{ padding: '0' }}>
                                    {balance.activo.lineas.map((linea, i) => (
                                        <div key={i} style={{ display: 'flex', justifyContent: 'space-between', padding: '10px 20px', borderBottom: '1px solid var(--border)', fontSize: '12px' }}>
                                            <div>
                                                <div style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{linea.codigo}</div>
                                                <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{linea.descripcion}</div>
                                            </div>
                                            <div style={{ fontWeight: 600, textAlign: 'right' }}>{fmt(linea.monto)}</div>
                                        </div>
                                    ))}
                                </div>
                                <div style={{ padding: '12px 20px', background: 'var(--bg-secondary)', borderTop: '2px solid var(--border)', display: 'flex', justifyContent: 'space-between', fontSize: '13px', fontWeight: 700 }}>
                                    <span>TOTAL ACTIVO</span>
                                    <span>{fmt(balance.totalActivo)}</span>
                                </div>
                            </div>

                            {/* Pasivo + Patrimonio */}
                            <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
                                <div className="erp-card">
                                    <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)' }}>
                                        <h3 style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-primary)' }}>
                                            {balance.pasivo.nombre}
                                        </h3>
                                    </div>
                                    <div style={{ padding: '0' }}>
                                        {balance.pasivo.lineas.map((linea, i) => (
                                            <div key={i} style={{ display: 'flex', justifyContent: 'space-between', padding: '10px 20px', borderBottom: '1px solid var(--border)', fontSize: '12px' }}>
                                                <div>
                                                    <div style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{linea.codigo}</div>
                                                    <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{linea.descripcion}</div>
                                                </div>
                                                <div style={{ fontWeight: 600, textAlign: 'right' }}>{fmt(linea.monto)}</div>
                                            </div>
                                        ))}
                                    </div>
                                    <div style={{ padding: '12px 20px', background: 'var(--bg-secondary)', borderTop: '2px solid var(--border)', display: 'flex', justifyContent: 'space-between', fontSize: '13px', fontWeight: 700 }}>
                                        <span>TOTAL PASIVO</span>
                                        <span>{fmt(balance.pasivo.total)}</span>
                                    </div>
                                </div>

                                <div className="erp-card">
                                    <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)' }}>
                                        <h3 style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-primary)' }}>
                                            {balance.patrimonio.nombre}
                                        </h3>
                                    </div>
                                    <div style={{ padding: '0' }}>
                                        {balance.patrimonio.lineas.map((linea, i) => (
                                            <div key={i} style={{ display: 'flex', justifyContent: 'space-between', padding: '10px 20px', borderBottom: '1px solid var(--border)', fontSize: '12px' }}>
                                                <div>
                                                    <div style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{linea.codigo}</div>
                                                    <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{linea.descripcion}</div>
                                                </div>
                                                <div style={{ fontWeight: 600, textAlign: 'right' }}>{fmt(linea.monto)}</div>
                                            </div>
                                        ))}
                                    </div>
                                    <div style={{ padding: '12px 20px', background: 'var(--bg-secondary)', borderTop: '2px solid var(--border)', display: 'flex', justifyContent: 'space-between', fontSize: '13px', fontWeight: 700 }}>
                                        <span>TOTAL PATRIMONIO</span>
                                        <span>{fmt(balance.patrimonio.total)}</span>
                                    </div>
                                </div>
                            </div>
                        </div>
                    )}

                    {/* PROFIT & LOSS */}
                    {tab === 'pyg' && pyg && (
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px', marginBottom: '20px' }}>
                            {/* Ingresos */}
                            <div className="erp-card">
                                <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)' }}>
                                    <h3 style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-primary)' }}>
                                        {pyg.ingresos.nombre}
                                    </h3>
                                </div>
                                <div style={{ padding: '0' }}>
                                    {pyg.ingresos.lineas.map((linea, i) => (
                                        <div key={i} style={{ display: 'flex', justifyContent: 'space-between', padding: '10px 20px', borderBottom: '1px solid var(--border)', fontSize: '12px' }}>
                                            <div>
                                                <div style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{linea.codigo}</div>
                                                <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{linea.descripcion}</div>
                                            </div>
                                            <div style={{ fontWeight: 600, textAlign: 'right' }}>{fmt(linea.monto)}</div>
                                        </div>
                                    ))}
                                </div>
                                <div style={{ padding: '12px 20px', background: 'var(--bg-secondary)', borderTop: '2px solid var(--border)', display: 'flex', justifyContent: 'space-between', fontSize: '13px', fontWeight: 700 }}>
                                    <span>TOTAL INGRESOS</span>
                                    <span>{fmt(pyg.totalIngresos)}</span>
                                </div>
                            </div>

                            {/* Gastos */}
                            <div className="erp-card">
                                <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)' }}>
                                    <h3 style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-primary)' }}>
                                        {pyg.gastos.nombre}
                                    </h3>
                                </div>
                                <div style={{ padding: '0' }}>
                                    {pyg.gastos.lineas.map((linea, i) => (
                                        <div key={i} style={{ display: 'flex', justifyContent: 'space-between', padding: '10px 20px', borderBottom: '1px solid var(--border)', fontSize: '12px' }}>
                                            <div>
                                                <div style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{linea.codigo}</div>
                                                <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{linea.descripcion}</div>
                                            </div>
                                            <div style={{ fontWeight: 600, textAlign: 'right' }}>{fmt(linea.monto)}</div>
                                        </div>
                                    ))}
                                </div>
                                <div style={{ padding: '12px 20px', background: 'var(--bg-secondary)', borderTop: '2px solid var(--border)', display: 'flex', justifyContent: 'space-between', fontSize: '13px', fontWeight: 700 }}>
                                    <span>TOTAL GASTOS</span>
                                    <span>{fmt(pyg.totalGastos)}</span>
                                </div>
                            </div>
                        </div>
                    )}

                    {tab === 'pyg' && pyg && (
                        <div className="erp-card" style={{ marginTop: '20px' }}>
                            <div style={{ padding: '20px', display: 'flex', justifyContent: 'space-around' }}>
                                <div style={{ textAlign: 'center' }}>
                                    <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '8px' }}>INGRESOS</div>
                                    <div style={{ fontSize: '18px', fontWeight: 700, color: '#065f46' }}>{fmt(pyg.totalIngresos)}</div>
                                </div>
                                <div style={{ borderLeft: '1px solid var(--border)' }}></div>
                                <div style={{ textAlign: 'center' }}>
                                    <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '8px' }}>GASTOS</div>
                                    <div style={{ fontSize: '18px', fontWeight: 700, color: '#991b1b' }}>{fmt(pyg.totalGastos)}</div>
                                </div>
                                <div style={{ borderLeft: '1px solid var(--border)' }}></div>
                                <div style={{ textAlign: 'center' }}>
                                    <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '8px' }}>RESULTADO NETO</div>
                                    <div style={{ fontSize: '18px', fontWeight: 700, color: pyg.resultadoNeto >= 0 ? '#065f46' : '#991b1b' }}>{fmt(pyg.resultadoNeto)}</div>
                                </div>
                            </div>
                        </div>
                    )}
                </>
            )}
        </PageContainer>
    );
}
