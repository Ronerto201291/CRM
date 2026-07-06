'use client';



import { useCallback, useState, Fragment } from 'react';

import PageContainer from '@/components/PageContainer';

import LegalDisclaimer, {

    PAYROLL_EXPORT_DISCLAIMER_TEXT,

    PAYROLL_MODULE_DISCLAIMER_TEXT,

} from '@/components/LegalDisclaimer';

import { downloadFiscalExport } from '@/lib/fiscalExportDownload';

import {

    payrollCalculateLineSchema,

    payrollEmployeeSchema,

    payrollManualLineSchema,

    payrollModel111ExportSchema,

    payrollModel190ExportSchema,

    payrollSettlementSchema,

    payrollTemplateSchema,

} from '@/lib/schemas/payrollFormSchemas';



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



interface SettlementLine {

    id: string;

    employeeId: string;

    employeeName: string;

    taxId: string;

    grossSalary: number;

    irpfWithheld: number;

    netPay: number;

}



interface Template {

    id: string;

    name: string;

    employeeSsRatePercent: number;

    employerSsRatePercent: number;

    defaultIrpfRatePercent: number;

    isDefault: boolean;

}



export default function PayrollClient({

    initialEmployees,

    initialSettlements,

    initialTemplates,

    initialYear,

}: {

    initialEmployees: Employee[];

    initialSettlements: Settlement[];

    initialTemplates: Template[];

    initialYear: number;

}) {

    const [employees, setEmployees] = useState<Employee[]>(initialEmployees);

    const [settlements, setSettlements] = useState<Settlement[]>(initialSettlements);

    const [templates, setTemplates] = useState<Template[]>(initialTemplates);

    const [year, setYear] = useState(initialYear);

    const [loading, setLoading] = useState(false);

    const [formError, setFormError] = useState<string | null>(null);

    const [actionError, setActionError] = useState<string | null>(null);

    const [exportDisclaimer, setExportDisclaimer] = useState<string | null>(null);

    const [calcNote, setCalcNote] = useState<string | null>(null);

    const [expandedSettlement, setExpandedSettlement] = useState<string | null>(null);

    const [settlementLines, setSettlementLines] = useState<SettlementLine[]>([]);

    const [model111Quarter, setModel111Quarter] = useState(Math.ceil((new Date().getMonth() + 1) / 3));

    const [empForm, setEmpForm] = useState({ taxId: '', fullName: '', ss: '' });

    const [setForm, setSetForm] = useState({ year: new Date().getFullYear(), month: new Date().getMonth() + 1 });

    const [lineForm, setLineForm] = useState({

        settlementId: '',

        employeeId: '',

        gross: '1500',

        templateId: '',

        irpfOverride: '',

    });

    const [manualLineForm, setManualLineForm] = useState({

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

    const [templateForm, setTemplateForm] = useState({

        name: 'Plantilla estándar',

        empSs: '6.35',

        erSs: '30',

        irpf: '15',

    });



    const load = useCallback(async () => {

        const [r1, r2, r3] = await Promise.all([

            fetch('/api/proxy/payroll/employees'),

            fetch(`/api/proxy/payroll/settlements?year=${year}`),

            fetch('/api/proxy/payroll/templates'),

        ]);

        if (r1.ok) setEmployees(await r1.json());

        if (r2.ok) setSettlements(await r2.json());

        if (r3.ok) setTemplates(await r3.json());

    }, [year]);



    const loadSettlementLines = async (settlementId: string) => {

        if (expandedSettlement === settlementId) {

            setExpandedSettlement(null);

            setSettlementLines([]);

            return;

        }

        const r = await fetch(`/api/proxy/payroll/settlements/${settlementId}/lines`);

        if (r.ok) {

            setSettlementLines(await r.json());

            setExpandedSettlement(settlementId);

        }

    };



    const downloadLinePdf = async (lineId: string, employeeName: string) => {

        setActionError(null);

        const r = await fetch(`/api/proxy/payroll/lines/${lineId}/pdf`);

        if (!r.ok) {

            setActionError('No se pudo generar el PDF del recibo');

            return;

        }

        const blob = await r.blob();

        const url = URL.createObjectURL(blob);

        const a = document.createElement('a');

        a.href = url;

        a.download = `Recibo_${employeeName.replace(/\s+/g, '_')}.pdf`;

        document.body.appendChild(a);

        a.click();

        document.body.removeChild(a);

        URL.revokeObjectURL(url);

    };



    const addEmployee = async () => {

        setFormError(null);

        const parsed = payrollEmployeeSchema.safeParse(empForm);

        if (!parsed.success) {

            setFormError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');

            return;

        }

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

            else setFormError('Error al crear empleado');

        } finally { setLoading(false); }

    };



    const createSettlement = async () => {

        setActionError(null);

        const parsed = payrollSettlementSchema.safeParse(setForm);

        if (!parsed.success) {

            setActionError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');

            return;

        }

        setLoading(true);

        try {

            const r = await fetch('/api/proxy/payroll/settlements', {

                method: 'POST',

                headers: { 'Content-Type': 'application/json' },

                body: JSON.stringify({ year: setForm.year, month: setForm.month }),

            });

            if (r.ok) load();

            else if (r.status === 409) setActionError('Ya existe liquidación para ese mes');

            else setActionError('Error al crear liquidación');

        } finally { setLoading(false); }

    };



    const calculateLine = async () => {

        setFormError(null);

        setCalcNote(null);

        const parsed = payrollCalculateLineSchema.safeParse(lineForm);

        if (!parsed.success) {

            setFormError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');

            return;

        }

        setLoading(true);

        try {

            const body: Record<string, unknown> = {

                employeeId: lineForm.employeeId,

                grossSalary: parseFloat(lineForm.gross),

            };

            if (lineForm.templateId) body.templateId = lineForm.templateId;

            if (lineForm.irpfOverride) body.irpfRatePercentOverride = parseFloat(lineForm.irpfOverride);



            const r = await fetch(`/api/proxy/payroll/settlements/${lineForm.settlementId}/calculate-line`, {

                method: 'POST',

                headers: { 'Content-Type': 'application/json' },

                body: JSON.stringify(body),

            });

            if (r.ok) {

                const data = await r.json();

                setCalcNote(data.calculationNote ?? 'Línea calculada.');

                load();

            } else {

                const e = await r.json().catch(() => ({}));

                setFormError(e.error || 'Error al calcular línea');

            }

        } finally { setLoading(false); }

    };



    const addLineManual = async () => {

        setFormError(null);

        const parsed = payrollManualLineSchema.safeParse(manualLineForm);

        if (!parsed.success) {

            setFormError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');

            return;

        }

        setLoading(true);

        try {

            const r = await fetch(`/api/proxy/payroll/settlements/${manualLineForm.settlementId}/lines`, {

                method: 'POST',

                headers: { 'Content-Type': 'application/json' },

                body: JSON.stringify({

                    employeeId: manualLineForm.employeeId,

                    grossSalary: parseFloat(manualLineForm.gross),

                    commonContingenciesBase: parseFloat(manualLineForm.baseCc),

                    employeeSocialSecurity: parseFloat(manualLineForm.empSs),

                    employerSocialSecurity: parseFloat(manualLineForm.erSs),

                    irpfBase: parseFloat(manualLineForm.irpfBase),

                    irpfRate: parseFloat(manualLineForm.irpfRate),

                    irpfWithheld: parseFloat(manualLineForm.irpfW),

                    netPay: parseFloat(manualLineForm.net),

                }),

            });

            if (r.ok) load();

            else { const e = await r.json().catch(() => ({})); setFormError(e.error || 'Error línea'); }

        } finally { setLoading(false); }

    };



    const createTemplate = async () => {

        setActionError(null);

        const parsed = payrollTemplateSchema.safeParse(templateForm);

        if (!parsed.success) {

            setActionError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');

            return;

        }

        setLoading(true);

        try {

            const r = await fetch('/api/proxy/payroll/templates', {

                method: 'POST',

                headers: { 'Content-Type': 'application/json' },

                body: JSON.stringify({

                    name: templateForm.name,

                    employeeSsRatePercent: parseFloat(templateForm.empSs),

                    employerSsRatePercent: parseFloat(templateForm.erSs),

                    defaultIrpfRatePercent: parseFloat(templateForm.irpf),

                    isDefault: templates.length === 0,

                }),

            });

            if (r.ok) load();

            else setActionError('Error al crear plantilla');

        } finally { setLoading(false); }

    };



    const finalize = async (id: string) => {

        if (!confirm('¿Cerrar liquidación? No se podrán añadir líneas.')) return;

        await fetch(`/api/proxy/payroll/settlements/${id}/finalize`, { method: 'POST' });

        load();

    };



    const downloadExport = async (kind: 'tc1' | 'tc2' | 'red' | 'tc-red-orientativo') => {

        setActionError(null);

        const path = kind === 'tc-red-orientativo' ? 'tc-red-orientativo' : kind;

        const ext = kind === 'red' || kind === 'tc-red-orientativo' ? 'txt' : 'csv';

        const label = kind === 'tc-red-orientativo' ? 'RED-XML' : kind.toUpperCase();

        const url = `/api/proxy/payroll/export/${path}?year=${year}&month=${setForm.month}`;

        const result = await downloadFiscalExport(

            url,

            `${label}_${year}_${String(setForm.month).padStart(2, '0')}.${ext}`,

        );

        if (!result.ok) {

            setActionError('Sin datos, liquidación no finalizada o validación RED fallida');

            return;

        }

        setExportDisclaimer(result.disclaimer);

    };



    const downloadModel111 = async () => {

        setActionError(null);

        const parsed = payrollModel111ExportSchema.safeParse({ year, quarter: model111Quarter });

        if (!parsed.success) {

            setActionError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');

            return;

        }

        const url = `/api/proxy/payroll/export/model-111?year=${year}&quarter=${model111Quarter}`;

        const result = await downloadFiscalExport(url, `Modelo111_orientativo_${year}_T${model111Quarter}.csv`);

        if (!result.ok) setActionError('Sin datos de nómina finalizadas para ese trimestre');

        else setExportDisclaimer(result.disclaimer);

    };



    const downloadModel190 = async () => {

        setActionError(null);

        const parsed = payrollModel190ExportSchema.safeParse({ year });

        if (!parsed.success) {

            setActionError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');

            return;

        }

        const url = `/api/proxy/payroll/export/model-190?year=${year}`;

        const result = await downloadFiscalExport(url, `Modelo190_orientativo_${year}.csv`);

        if (!result.ok) setActionError('Sin datos de nómina finalizadas para ese ejercicio');

        else setExportDisclaimer(result.disclaimer);

    };



    return (

        <PageContainer>

            <div className="page-header">

                <div>

                    <h1 className="page-title">Nóminas (Fase 2)</h1>

                    <p className="page-subtitle">Recibos PDF, modelos 111/190 orientativos, plantillas, cálculo y export TGSS</p>

                </div>

            </div>



            <LegalDisclaimer title="Aviso legal — módulo de nóminas" testId="legal-disclaimer-payroll-module">

                {PAYROLL_MODULE_DISCLAIMER_TEXT}

            </LegalDisclaimer>



            {(formError || actionError) && (

                <div className="erp-card" style={{ padding: '12px 16px', marginBottom: 16, color: 'var(--danger)', background: 'var(--danger-bg)' }}>

                    {formError || actionError}

                </div>

            )}



            {calcNote && (

                <div className="erp-card" style={{ padding: '12px 16px', marginBottom: 16, color: 'var(--info)', background: 'var(--info-bg)' }}>

                    {calcNote}

                </div>

            )}



            <div className="erp-card" style={{ padding: '20px', marginBottom: '16px' }}>

                <h3 style={{ fontSize: '14px', fontWeight: 700, marginBottom: '12px' }}>Plantillas de cálculo</h3>

                <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '10px' }}>

                    Porcentajes SS e IRPF configurables — no sustituyen tablas oficiales ni convenio colectivo.

                </p>

                {templates.length > 0 && (

                    <ul style={{ fontSize: '13px', marginBottom: '12px' }}>

                        {templates.map(t => (

                            <li key={t.id}>

                                {t.name} — SS {t.employeeSsRatePercent}% / {t.employerSsRatePercent}%, IRPF {t.defaultIrpfRatePercent}%

                                {t.isDefault ? ' (por defecto)' : ''}

                            </li>

                        ))}

                    </ul>

                )}

                <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', alignItems: 'flex-end' }}>

                    <input className="erp-input" placeholder="Nombre" value={templateForm.name}

                        onChange={e => setTemplateForm({ ...templateForm, name: e.target.value })} />

                    <input className="erp-input" placeholder="SS obrera %" value={templateForm.empSs}

                        onChange={e => setTemplateForm({ ...templateForm, empSs: e.target.value })} style={{ maxWidth: '100px' }} />

                    <input className="erp-input" placeholder="SS empresa %" value={templateForm.erSs}

                        onChange={e => setTemplateForm({ ...templateForm, erSs: e.target.value })} style={{ maxWidth: '100px' }} />

                    <input className="erp-input" placeholder="IRPF %" value={templateForm.irpf}

                        onChange={e => setTemplateForm({ ...templateForm, irpf: e.target.value })} style={{ maxWidth: '80px' }} />

                    <button className="btn btn-secondary btn-sm" onClick={createTemplate} disabled={loading}>Crear plantilla</button>

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

                <div style={{ display: 'flex', gap: '10px', alignItems: 'center', marginBottom: '12px', flexWrap: 'wrap' }}>

                    <label>Año <input type="number" className="erp-input" style={{ width: '90px' }} value={setForm.year} onChange={e => setSetForm({ ...setForm, year: +e.target.value })} /></label>

                    <label>Mes <input type="number" min={1} max={12} className="erp-input" style={{ width: '70px' }} value={setForm.month} onChange={e => setSetForm({ ...setForm, month: +e.target.value })} /></label>

                    <button className="btn btn-secondary btn-sm" onClick={createSettlement} disabled={loading}>Crear borrador</button>

                    <label style={{ marginLeft: '16px' }}>Ver año <input type="number" className="erp-input" style={{ width: '90px' }} value={year} onChange={e => setYear(+e.target.value)} /></label>

                </div>

                <table style={{ width: '100%', fontSize: '13px' }}>

                    <thead><tr style={{ textAlign: 'left' }}><th>Mes</th><th>Estado</th><th>Líneas</th><th>Bruto</th><th>IRPF</th><th>SS empresa</th><th></th></tr></thead>

                    <tbody>

                        {settlements.map(s => (

                            <Fragment key={s.id}>

                                <tr>

                                    <td>{s.month}/{s.year}</td><td>{s.status}</td><td>{s.lineCount}</td>

                                    <td>€{(s.totalGross ?? 0).toFixed(2)}</td>

                                    <td>€{(s.totalIrpf ?? 0).toFixed(2)}</td>

                                    <td>€{(s.totalEmployerSs ?? 0).toFixed(2)}</td>

                                    <td style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>

                                        {s.lineCount > 0 && (

                                            <button type="button" className="btn btn-secondary btn-sm" onClick={() => loadSettlementLines(s.id)}>

                                                {expandedSettlement === s.id ? 'Ocultar' : 'Recibos PDF'}

                                            </button>

                                        )}

                                        {s.status === 'Draft' && (

                                            <button type="button" className="btn btn-secondary btn-sm" onClick={() => finalize(s.id)}>Finalizar</button>

                                        )}

                                    </td>

                                </tr>

                                {expandedSettlement === s.id && settlementLines.length > 0 && (

                                    <tr key={`${s.id}-lines`}>

                                        <td colSpan={7} style={{ padding: '8px 0' }}>

                                            <table style={{ width: '100%', fontSize: '12px', background: 'var(--surface-2)' }}>

                                                <thead><tr><th>Empleado</th><th>NIF</th><th>Líquido</th><th></th></tr></thead>

                                                <tbody>

                                                    {settlementLines.map(l => (

                                                        <tr key={l.id}>

                                                            <td>{l.employeeName}</td>

                                                            <td>{l.taxId}</td>

                                                            <td>€{l.netPay.toFixed(2)}</td>

                                                            <td>

                                                                <button type="button" className="btn btn-secondary btn-sm"

                                                                    onClick={() => downloadLinePdf(l.id, l.employeeName)}>

                                                                    Descargar PDF

                                                                </button>

                                                            </td>

                                                        </tr>

                                                    ))}

                                                </tbody>

                                            </table>

                                        </td>

                                    </tr>

                                )}

                            </Fragment>

                        ))}

                    </tbody>

                </table>

            </div>



            <div className="erp-card" style={{ padding: '20px', marginBottom: '16px' }}>

                <h3 style={{ fontSize: '14px', fontWeight: 700, marginBottom: '12px' }}>Calcular línea automática</h3>

                <LegalDisclaimer title="Importes calculados — revisión obligatoria" variant="info" testId="legal-disclaimer-payroll-calc">

                    Los importes se calculan con las plantillas configuradas arriba. No sustituyen asesoría laboral ni tablas oficiales TGSS/AEAT.

                </LegalDisclaimer>

                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(140px, 1fr))', gap: '8px', marginBottom: '10px' }}>

                    <input className="erp-input" placeholder="ID liquidación" value={lineForm.settlementId} onChange={e => setLineForm({ ...lineForm, settlementId: e.target.value })} />

                    <select className="erp-input" value={lineForm.employeeId} onChange={e => setLineForm({ ...lineForm, employeeId: e.target.value })}>

                        <option value="">Empleado…</option>

                        {employees.map(e => <option key={e.id} value={e.id}>{e.fullName}</option>)}

                    </select>

                    <input className="erp-input" placeholder="Bruto" value={lineForm.gross} onChange={e => setLineForm({ ...lineForm, gross: e.target.value })} />

                    <select className="erp-input" value={lineForm.templateId} onChange={e => setLineForm({ ...lineForm, templateId: e.target.value })}>

                        <option value="">Plantilla por defecto</option>

                        {templates.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}

                    </select>

                    <input className="erp-input" placeholder="IRPF % override" value={lineForm.irpfOverride} onChange={e => setLineForm({ ...lineForm, irpfOverride: e.target.value })} />

                </div>

                <button className="btn btn-primary btn-sm" onClick={calculateLine} disabled={loading}>Calcular y añadir línea</button>

            </div>



            <div className="erp-card" style={{ padding: '20px', marginBottom: '16px' }}>

                <h3 style={{ fontSize: '14px', fontWeight: 700, marginBottom: '12px' }}>Línea manual (importes externos)</h3>

                <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '8px' }}>

                    Introduzca importes ya calculados por su gestoría. Usted es responsable de su exactitud.

                </p>

                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(140px, 1fr))', gap: '8px', marginBottom: '10px' }}>

                    <input className="erp-input" placeholder="ID liquidación" value={manualLineForm.settlementId} onChange={e => setManualLineForm({ ...manualLineForm, settlementId: e.target.value })} />

                    <select className="erp-input" value={manualLineForm.employeeId} onChange={e => setManualLineForm({ ...manualLineForm, employeeId: e.target.value })}>

                        <option value="">Empleado…</option>

                        {employees.map(e => <option key={e.id} value={e.id}>{e.fullName}</option>)}

                    </select>

                    <input className="erp-input" placeholder="Bruto" value={manualLineForm.gross} onChange={e => setManualLineForm({ ...manualLineForm, gross: e.target.value })} />

                    <input className="erp-input" placeholder="Base CC" value={manualLineForm.baseCc} onChange={e => setManualLineForm({ ...manualLineForm, baseCc: e.target.value })} />

                    <input className="erp-input" placeholder="SS obrera" value={manualLineForm.empSs} onChange={e => setManualLineForm({ ...manualLineForm, empSs: e.target.value })} />

                    <input className="erp-input" placeholder="SS empresa" value={manualLineForm.erSs} onChange={e => setManualLineForm({ ...manualLineForm, erSs: e.target.value })} />

                    <input className="erp-input" placeholder="Base IRPF" value={manualLineForm.irpfBase} onChange={e => setManualLineForm({ ...manualLineForm, irpfBase: e.target.value })} />

                    <input className="erp-input" placeholder="% IRPF" value={manualLineForm.irpfRate} onChange={e => setManualLineForm({ ...manualLineForm, irpfRate: e.target.value })} />

                    <input className="erp-input" placeholder="IRPF retenido" value={manualLineForm.irpfW} onChange={e => setManualLineForm({ ...manualLineForm, irpfW: e.target.value })} />

                    <input className="erp-input" placeholder="Líquido" value={manualLineForm.net} onChange={e => setManualLineForm({ ...manualLineForm, net: e.target.value })} />

                </div>

                <button className="btn btn-secondary btn-sm" onClick={addLineManual} disabled={loading}>Añadir línea manual</button>

            </div>



            <div className="erp-card" style={{ padding: '20px', marginBottom: '16px' }}>

                <h3 style={{ fontSize: '14px', fontWeight: 700, marginBottom: '12px' }}>Modelos 111 / 190 (orientativos)</h3>

                <LegalDisclaimer title="Modelos AEAT — no presentables" variant="warning" testId="legal-disclaimer-payroll-111-190">

                    Los exportes 111 y 190 se generan desde datos de nómina del ERP. Son orientativos y no sustituyen

                    la presentación oficial ante la AEAT ni la validación por un asesor fiscal.

                </LegalDisclaimer>

                <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap', alignItems: 'center', marginBottom: '10px' }}>

                    <label>Trimestre 111

                        <select className="erp-input" value={model111Quarter} onChange={e => setModel111Quarter(+e.target.value)} style={{ marginLeft: '6px' }}>

                            <option value={1}>T1</option><option value={2}>T2</option>

                            <option value={3}>T3</option><option value={4}>T4</option>

                        </select>

                    </label>

                    <button type="button" className="btn btn-secondary btn-sm" onClick={downloadModel111}>

                        Exportar Modelo 111 {year} T{model111Quarter}

                    </button>

                    <button type="button" className="btn btn-secondary btn-sm" onClick={downloadModel190}>

                        Exportar Modelo 190 {year}

                    </button>

                </div>

            </div>



            <div className="erp-card" style={{ padding: '20px' }}>

                <h3 style={{ fontSize: '14px', fontWeight: 700, marginBottom: '12px' }}>Export TGSS (orientativo)</h3>

                <LegalDisclaimer title="Exportes TGSS — no homologados" testId="legal-disclaimer-payroll-export">

                    {PAYROLL_EXPORT_DISCLAIMER_TEXT}

                </LegalDisclaimer>

                {exportDisclaimer && (

                    <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '10px', fontStyle: 'italic' }}>

                        Última descarga: {exportDisclaimer}

                    </p>

                )}

                <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>

                    <button type="button" className="btn btn-secondary btn-sm" onClick={() => downloadExport('tc1')}>TC1 {year}-{setForm.month}</button>

                    <button type="button" className="btn btn-secondary btn-sm" onClick={() => downloadExport('tc2')}>TC2 {year}-{setForm.month}</button>

                    <button type="button" className="btn btn-primary btn-sm" onClick={() => downloadExport('red')}>RED/SILTRA {year}-{setForm.month}</button>

                    <button type="button" className="btn btn-secondary btn-sm" onClick={() => downloadExport('tc-red-orientativo')}>RED XML {year}-{setForm.month}</button>

                </div>

            </div>

        </PageContainer>

    );

}

