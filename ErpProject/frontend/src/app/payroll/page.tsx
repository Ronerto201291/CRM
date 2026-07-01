'use client';

import { useCallback, useEffect, useState } from 'react';
import PageContainer from '@/components/PageContainer';

interface Employee {
    id: string;
    taxId: string;
    fullName: string;
    socialSecurityNumber?: string;
    hireDate: string;
    contractType: string;
    weeklyHours: number;
}

interface Settlement {
    id: string;
    year: number;
    month: number;
    status: string;
    lineCount: number;
    totalGross: number;
    totalIrpf: number;
    totalEmployerSs: number;
}

export default function PayrollPage() {
    const [employees, setEmployees] = useState<Employee[]>([]);
    const [settlements, setSettlements] = useState<Settlement[]>([]);
    const [year, setYear] = useState(new Date().getFullYear());
    const [loading, setLoading] = useState(false);
    const [empForm, setEmpForm] = useState({ taxId: '', fullName: '', ss: '' });
    const [setForm, setSetForm] = useState({ year: new Date().getFullYear(), month: new Date().getMonth() + 1 });
    const [lineForm, setLineForm] = useState({
        settlementId: '',
        employeeId: '',
        gross: '1500',
        baseCc: '1500',
        empSs: '100',
        erSs: '450',
        irpfBase: '1500',
        irpfRate: '15',
        irpfW: '225',
        net: '1175',
    });

    const load = useCallback(async () => {
        const [r1, r2] = await Promise.all([
            fetch('/api/proxy/payroll/employees'),
            fetch(`/api/proxy/payroll/settlements?year=${year}`),
        ]);
        if (r1.ok) setEmployees(await r1.json());
        if (r2.ok) setSettlements(await r2.json());
    }, [year]);

    useEffect(() => { load(); }, [load]);

    const addEmployee = async () => {
        if (!empForm.taxId || !empForm.fullName) { alert('NIF y nombre obligatorios'); return; }
        setLoading(true);
        try {
            const r = await fetch('/api/proxy/payroll/employees', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    taxId: empForm.taxId,
                    fullName: empForm.fullName,
                    socialSecurityNumber: empForm.ss || null,
                }),
            });
            if (r.ok) { setEmpForm({ taxId: '', fullName: '', ss: '' }); load(); }
            else alert('Error al crear empleado');
        } finally { setLoading(false); }
    };

    const createSettlement = async () => {
        setLoading(true);
        try {
            const r = await fetch('/api/proxy/payroll/settlements', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ year: setForm.year, month: setForm.month }),
            });
            if (r.ok) load();
            else if (r.status === 409) alert('Ya existe liquidación para ese mes');
            else alert('Error');
        } finally { setLoading(false); }
    };

    const addLine = async () => {
        if (!lineForm.settlementId || !lineForm.employeeId) { alert('Selecciona liquidación y empleado'); return; }
        setLoading(true);
        try {
            const r = await fetch(`/api/proxy/payroll/settlements/${lineForm.settlementId}/lines`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    employeeId: lineForm.employeeId,
                    grossSalary: parseFloat(lineForm.gross),
                    commonContingenciesBase: parseFloat(lineForm.baseCc),
                    employeeSocialSecurity: parseFloat(lineForm.empSs),
                    employerSocialSecurity: parseFloat(lineForm.erSs),
                    irpfBase: parseFloat(lineForm.irpfBase),
                    irpfRate: parseFloat(lineForm.irpfRate),
                    irpfWithheld: parseFloat(lineForm.irpfW),
                    netPay: parseFloat(lineForm.net),
                }),
            });
            if (r.ok) load();
            else { const e = await r.json().catch(() => ({})); alert(e.error || 'Error línea'); }
        } finally { setLoading(false); }
    };

    const finalize = async (id: string) => {
        if (!confirm('¿Cerrar liquidación? No se podrán añadir líneas.')) return;
        await fetch(`/api/proxy/payroll/settlements/${id}/finalize`, { method: 'POST' });
        load();
    };

    const downloadTc = async (kind: 'tc1' | 'tc2') => {
        const url = `/api/proxy/payroll/export/${kind}?year=${year}&month=${setForm.month}`;
        const r = await fetch(url);
        if (!r.ok) { alert('Sin datos o liquidación no finalizada'); return; }
        const blob = await r.blob();
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = `${kind.toUpperCase()}_${year}_${String(setForm.month).padStart(2, '0')}.csv`;
        a.click();
        URL.revokeObjectURL(a.href);
    };

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Nóminas (Fase 0)</h1>
                    <p className="page-subtitle">Trabajadores, liquidaciones mensuales, TC1/TC2 CSV y datos para modelo 111/190</p>
                </div>
            </div>

            <div className="erp-card" style={{ padding: '20px', marginBottom: '16px' }}>
                <h3 style={{ fontSize: '14px', fontWeight: 700, marginBottom: '12px' }}>Nuevo empleado</h3>
                <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap', alignItems: 'flex-end' }}>
                    <input className="erp-input" placeholder="NIF" value={empForm.taxId} onChange={e => setEmpForm({ ...empForm, taxId: e.target.value })} style={{ maxWidth: '140px' }} />
                    <input className="erp-input" placeholder="Nombre completo" value={empForm.fullName} onChange={e => setEmpForm({ ...empForm, fullName: e.target.value })} style={{ flex: 1, minWidth: '200px' }} />
                    <input className="erp-input" placeholder="NAF (opcional)" value={empForm.ss} onChange={e => setEmpForm({ ...empForm, ss: e.target.value })} style={{ maxWidth: '160px' }} />
                    <button className="btn btn-primary btn-sm" onClick={addEmployee} disabled={loading}>Añadir</button>
                </div>
            </div>

            <div className="erp-card" style={{ padding: '20px', marginBottom: '16px' }}>
                <h3 style={{ fontSize: '14px', fontWeight: 700, marginBottom: '12px' }}>Empleados activos</h3>
                <table style={{ width: '100%', fontSize: '13px' }}>
                    <thead><tr style={{ textAlign: 'left' }}><th>NIF</th><th>Nombre</th><th>NAF</th></tr></thead>
                    <tbody>
                        {employees.map(e => (
                            <tr key={e.id}><td>{e.taxId}</td><td>{e.fullName}</td><td>{e.socialSecurityNumber ?? '—'}</td></tr>
                        ))}
                    </tbody>
                </table>
            </div>

            <div className="erp-card" style={{ padding: '20px', marginBottom: '16px' }}>
                <h3 style={{ fontSize: '14px', fontWeight: 700, marginBottom: '12px' }}>Liquidación mensual</h3>
                <div style={{ display: 'flex', gap: '10px', alignItems: 'center', marginBottom: '12px' }}>
                    <label>Año <input type="number" className="erp-input" style={{ width: '90px' }} value={setForm.year} onChange={e => setSetForm({ ...setForm, year: +e.target.value })} /></label>
                    <label>Mes <input type="number" min={1} max={12} className="erp-input" style={{ width: '70px' }} value={setForm.month} onChange={e => setSetForm({ ...setForm, month: +e.target.value })} /></label>
                    <button className="btn btn-secondary btn-sm" onClick={createSettlement} disabled={loading}>Crear borrador</button>
                    <label style={{ marginLeft: '16px' }}>Ver año <input type="number" className="erp-input" style={{ width: '90px' }} value={year} onChange={e => setYear(+e.target.value)} /></label>
                </div>
                <table style={{ width: '100%', fontSize: '13px' }}>
                    <thead><tr style={{ textAlign: 'left' }}><th>Mes</th><th>Estado</th><th>Líneas</th><th>Bruto</th><th>IRPF</th><th>SS empresa</th><th></th></tr></thead>
                    <tbody>
                        {settlements.map(s => (
                            <tr key={s.id}>
                                <td>{s.month}/{s.year}</td><td>{s.status}</td><td>{s.lineCount}</td>
                                <td>€{(s.totalGross ?? 0).toFixed(2)}</td>
                                <td>€{(s.totalIrpf ?? 0).toFixed(2)}</td>
                                <td>€{(s.totalEmployerSs ?? 0).toFixed(2)}</td>
                                <td>
                                    {s.status === 'Draft' && (
                                        <button type="button" className="btn btn-secondary btn-sm" onClick={() => finalize(s.id)}>Finalizar</button>
                                    )}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            <div className="erp-card" style={{ padding: '20px', marginBottom: '16px' }}>
                <h3 style={{ fontSize: '14px', fontWeight: 700, marginBottom: '12px' }}>Línea de nómina (borrador)</h3>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(140px, 1fr))', gap: '8px', marginBottom: '10px' }}>
                    <input className="erp-input" placeholder="ID liquidación" value={lineForm.settlementId} onChange={e => setLineForm({ ...lineForm, settlementId: e.target.value })} />
                    <select className="erp-input" value={lineForm.employeeId} onChange={e => setLineForm({ ...lineForm, employeeId: e.target.value })}>
                        <option value="">Empleado…</option>
                        {employees.map(e => <option key={e.id} value={e.id}>{e.fullName}</option>)}
                    </select>
                    <input className="erp-input" placeholder="Bruto" value={lineForm.gross} onChange={e => setLineForm({ ...lineForm, gross: e.target.value })} />
                    <input className="erp-input" placeholder="Base CC" value={lineForm.baseCc} onChange={e => setLineForm({ ...lineForm, baseCc: e.target.value })} />
                    <input className="erp-input" placeholder="SS obrera" value={lineForm.empSs} onChange={e => setLineForm({ ...lineForm, empSs: e.target.value })} />
                    <input className="erp-input" placeholder="SS empresa" value={lineForm.erSs} onChange={e => setLineForm({ ...lineForm, erSs: e.target.value })} />
                    <input className="erp-input" placeholder="Base IRPF" value={lineForm.irpfBase} onChange={e => setLineForm({ ...lineForm, irpfBase: e.target.value })} />
                    <input className="erp-input" placeholder="% IRPF" value={lineForm.irpfRate} onChange={e => setLineForm({ ...lineForm, irpfRate: e.target.value })} />
                    <input className="erp-input" placeholder="IRPF retenido" value={lineForm.irpfW} onChange={e => setLineForm({ ...lineForm, irpfW: e.target.value })} />
                    <input className="erp-input" placeholder="Líquido" value={lineForm.net} onChange={e => setLineForm({ ...lineForm, net: e.target.value })} />
                </div>
                <button className="btn btn-primary btn-sm" onClick={addLine} disabled={loading}>Añadir línea</button>
            </div>

            <div className="erp-card" style={{ padding: '20px' }}>
                <h3 style={{ fontSize: '14px', fontWeight: 700, marginBottom: '12px' }}>Export TGSS (orientativo)</h3>
                <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '10px' }}>Tras finalizar el mes, descarga CSV para asesoría. No sustituye SILTRA/RED sin validación.</p>
                <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                    <button type="button" className="btn btn-secondary btn-sm" onClick={() => downloadTc('tc1')}>TC1 {year}-{setForm.month}</button>
                    <button type="button" className="btn btn-secondary btn-sm" onClick={() => downloadTc('tc2')}>TC2 {year}-{setForm.month}</button>
                </div>
            </div>
        </PageContainer>
    );
}
