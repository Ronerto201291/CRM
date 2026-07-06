'use client';
import { useState } from 'react';
import PageContainer from '@/components/PageContainer';
import LegalDisclaimer, { FISCAL_EXPORT_DISCLAIMER_TEXT } from '@/components/LegalDisclaimer';

interface ViesResult {
    isValid: boolean;
    countryCode: string;
    vatNumber: string;
    name?: string;
    address?: string;
    requestDate?: string;
    errorMessage?: string;
    advice: string;
}

export default function AeatPage() {
    const year = new Date().getFullYear();
    const currentQ = Math.ceil((new Date().getMonth() + 1) / 3);

    const [selYear, setSelYear]   = useState(year);
    const [selQ, setSelQ]         = useState(currentQ);
    const [downloading, setDownloading] = useState<string | null>(null);
    const [declaring, setDeclaring] = useState(false);
    const [declaration, setDeclaration] = useState<{
        id: string;
        totalDevengado: number;
        ivaDeducible: number;
        resultado: number;
        resultadoTipo: string;
        message: string;
    } | null>(null);

    // VIES
    const [viesCountry, setViesCountry] = useState('');
    const [viesVat, setViesVat]         = useState('');
    const [viesLoading, setViesLoading] = useState(false);
    const [viesResult, setViesResult]   = useState<ViesResult | null>(null);

    const downloadFile = async (url: string, filename: string, key: string) => {
        setDownloading(key);
        try {
            const r = await fetch(url);
            if (!r.ok) { alert('Error al generar el archivo. Comprueba que existen datos en el período.'); return; }
            const blob = await r.blob();
            const a = document.createElement('a');
            a.href = URL.createObjectURL(blob); a.download = filename;
            document.body.appendChild(a); a.click();
            document.body.removeChild(a); URL.revokeObjectURL(a.href);
        } finally {
            setDownloading(null);
        }
    };

    const downloadJson = async (url: string, filename: string, key: string) => {
        setDownloading(key);
        try {
            const r = await fetch(url);
            if (!r.ok) { alert('Error al obtener datos.'); return; }
            const data = await r.json();
            const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
            const a = document.createElement('a');
            a.href = URL.createObjectURL(blob); a.download = filename;
            document.body.appendChild(a); a.click();
            document.body.removeChild(a); URL.revokeObjectURL(a.href);
        } finally {
            setDownloading(null);
        }
    };

    const validateVies = async () => {
        if (!viesCountry || viesCountry.length !== 2) { alert('Introduce un código de país ISO-2 (p.ej. FR, DE, IT)'); return; }
        if (!viesVat) { alert('Introduce el NIF/VAT del operador UE'); return; }
        setViesLoading(true);
        setViesResult(null);
        try {
            const r = await fetch(`/api/proxy/tax/vies/validate?countryCode=${encodeURIComponent(viesCountry)}&vatNumber=${encodeURIComponent(viesVat)}`);
            if (r.ok) setViesResult(await r.json());
            else { const e = await r.json(); alert(e.error || 'Error VIES'); }
        } catch { alert('Error de conexión con el servicio VIES'); }
        finally { setViesLoading(false); }
    };

    const declareModelo303 = async () => {
        setDeclaring(true);
        setDeclaration(null);
        try {
            const r = await fetch('/api/proxy/v1/accounting/vat/declare/modelo330', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ year: selYear, quarter: selQ }),
            });
            const data = await r.json().catch(() => ({}));
            if (!r.ok) {
                alert(data.error || 'Error al registrar la declaración IVA.');
                return;
            }
            setDeclaration({
                id: data.id,
                totalDevengado: data.totalDevengado,
                ivaDeducible: data.ivaDeducible,
                resultado: data.resultado,
                resultadoTipo: data.resultadoTipo,
                message: data.message,
            });
        } catch {
            alert('Error de conexión al registrar la declaración.');
        } finally {
            setDeclaring(false);
        }
    };

    const periodo = `${selYear} T${selQ}`;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Modelos AEAT</h1>
                    <p className="page-subtitle">Exportación XML oficial · FacturaE · VIES · Mod. 303, 349</p>
                </div>
            </div>

            <LegalDisclaimer title="Aviso legal — modelos AEAT y exportes fiscales">
                {FISCAL_EXPORT_DISCLAIMER_TEXT} Los modelos 303, 349, 347, 111, 190 y libros IVA son orientativos hasta validación con asesoría y programa AEAT oficial.
            </LegalDisclaimer>

            {/* Selector período */}
            <div className="erp-card" style={{ padding: '16px 20px', marginBottom: '20px', display: 'flex', gap: '16px', alignItems: 'flex-end' }}>
                <div>
                    <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px', letterSpacing: '0.04em' }}>EJERCICIO</label>
                    <select value={selYear} onChange={e => setSelYear(+e.target.value)}
                        style={{ padding: '7px 10px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '13px', background: 'var(--bg-secondary)', color: 'var(--text-primary)' }}>
                        {[year - 1, year].map(y => <option key={y} value={y}>{y}</option>)}
                    </select>
                </div>
                <div>
                    <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px', letterSpacing: '0.04em' }}>TRIMESTRE</label>
                    <select value={selQ} onChange={e => setSelQ(+e.target.value)}
                        style={{ padding: '7px 10px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '13px', background: 'var(--bg-secondary)', color: 'var(--text-primary)' }}>
                        <option value={1}>T1 — Ene/Mar</option>
                        <option value={2}>T2 — Abr/Jun</option>
                        <option value={3}>T3 — Jul/Sep</option>
                        <option value={4}>T4 — Oct/Dic</option>
                    </select>
                </div>
                <div style={{ fontSize: '13px', color: 'var(--text-muted)', paddingBottom: '8px' }}>
                    Período seleccionado: <strong style={{ color: 'var(--text-primary)' }}>{periodo}</strong>
                </div>
            </div>

            {/* Modelo 303 */}
            <div className="erp-card" style={{ marginBottom: '16px' }}>
                <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div>
                        <div style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-primary)' }}>Modelo 303 — IVA Trimestral</div>
                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>
                            Declaración-liquidación trimestral del Impuesto sobre el Valor Añadido
                        </div>
                    </div>
                    <div style={{ display: 'flex', gap: '8px' }}>
                        <button className="btn btn-secondary btn-sm"
                            disabled={declaring}
                            onClick={declareModelo303}>
                            {declaring ? '⏳' : '📝'} Registrar declaración
                        </button>
                        <button className="btn btn-secondary btn-sm"
                            disabled={downloading === 'm303csv'}
                            onClick={() => downloadFile(
                                `/api/proxy/accounting/export/modelo303?year=${selYear}&q=${selQ}`,
                                `Modelo303_${selYear}_T${selQ}.csv`, 'm303csv')}>
                            {downloading === 'm303csv' ? '⏳' : '⬇'} CSV
                        </button>
                        <button className="btn btn-primary btn-sm"
                            disabled={downloading === 'm303xml'}
                            onClick={() => downloadFile(
                                `/api/proxy/accounting/export/modelo303-xml?year=${selYear}&q=${selQ}`,
                                `Modelo303_${selYear}_T${selQ}.xml`, 'm303xml')}>
                            {downloading === 'm303xml' ? '⏳' : '⬇'} XML AEAT
                        </button>
                    </div>
                </div>
                <div style={{ padding: '12px 20px', fontSize: '12px', color: 'var(--text-muted)' }}>
                    Casillas 001–067 · IVA devengado, deducible y liquidación · Recargo de equivalencia
                </div>
                {declaration && (
                    <div style={{
                        margin: '0 20px 16px',
                        padding: '12px 16px',
                        borderRadius: '8px',
                        background: 'var(--success-bg)',
                        border: '1px solid #a7f3d0',
                        fontSize: '13px',
                    }}>
                        <div style={{ fontWeight: 700, color: '#065f46', marginBottom: '6px' }}>
                            Declaración registrada — {periodo}
                        </div>
                        <div>Devengado: <strong>{declaration.totalDevengado.toFixed(2)} €</strong></div>
                        <div>Deducible: <strong>{declaration.ivaDeducible.toFixed(2)} €</strong></div>
                        <div>
                            Resultado ({declaration.resultadoTipo}):{' '}
                            <strong>{declaration.resultado.toFixed(2)} €</strong>
                        </div>
                        <div style={{ marginTop: '6px', fontStyle: 'italic', color: '#065f46' }}>
                            {declaration.message}
                        </div>
                    </div>
                )}
            </div>

            {/* Modelo 349 */}
            <div className="erp-card" style={{ marginBottom: '16px' }}>
                <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div>
                        <div style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-primary)' }}>Modelo 349 — Operaciones Intracomunitarias</div>
                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>
                            Declaración recapitulativa de entregas y adquisiciones intracomunitarias (art. 164 LIVA)
                        </div>
                    </div>
                    <div style={{ display: 'flex', gap: '8px' }}>
                        <button className="btn btn-secondary btn-sm"
                            disabled={downloading === 'm349csv'}
                            onClick={() => downloadFile(
                                `/api/proxy/accounting/export/modelo349?year=${selYear}&q=${selQ}&format=csv`,
                                `Modelo349_${selYear}_T${selQ}.csv`, 'm349csv')}>
                            {downloading === 'm349csv' ? '⏳' : '⬇'} CSV
                        </button>
                        <button className="btn btn-primary btn-sm"
                            disabled={downloading === 'm349xml'}
                            onClick={() => downloadFile(
                                `/api/proxy/accounting/export/modelo349?year=${selYear}&q=${selQ}`,
                                `Modelo349_${selYear}_T${selQ}.xml`, 'm349xml')}>
                            {downloading === 'm349xml' ? '⏳' : '⬇'} XML AEAT
                        </button>
                    </div>
                </div>
                <div style={{ padding: '12px 20px', fontSize: '12px', color: 'var(--text-muted)' }}>
                    Claves E (entregas), A (adquisiciones) · Periodicidad trimestral o mensual si &gt; 50.000 €
                </div>
            </div>

            {/* Libro Diario CSV */}
            <div className="erp-card" style={{ marginBottom: '16px' }}>
                <div style={{ padding: '16px 20px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div>
                        <div style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-primary)' }}>Libro Diario</div>
                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>
                            Exportación CSV de todos los asientos del ejercicio (PGC 2007)
                        </div>
                    </div>
                    <button className="btn btn-secondary btn-sm"
                        disabled={downloading === 'diario'}
                        onClick={() => downloadFile(
                            `/api/proxy/accounting/export/libro-diario?year=${selYear}`,
                            `LibroDiario_${selYear}.csv`, 'diario')}>
                        {downloading === 'diario' ? '⏳' : '⬇'} CSV
                    </button>
                </div>
            </div>

            {/* Libros registro IVA (RIVA) */}
            <div className="erp-card" style={{ marginBottom: '16px' }}>
                <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)' }}>
                    <div style={{ fontSize: '14px', fontWeight: 700 }}>Libros registro IVA (Art. 63–64 RIVA)</div>
                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>CSV con campos habituales emitidas / recibidas · validar formato ante inspección</div>
                </div>
                <div style={{ padding: '12px 20px', display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                    <button className="btn btn-secondary btn-sm" disabled={downloading === 'lemit'}
                        onClick={() => downloadFile(`/api/proxy/accounting/export/libro-iva-emitidas?year=${selYear}`, `LibroIVA_Emitidas_${selYear}.csv`, 'lemit')}>
                        {downloading === 'lemit' ? '⏳' : '⬇'} Emitidas
                    </button>
                    <button className="btn btn-secondary btn-sm" disabled={downloading === 'lrec'}
                        onClick={() => downloadFile(`/api/proxy/accounting/export/libro-iva-recibidas?year=${selYear}`, `LibroIVA_Recibidas_${selYear}.csv`, 'lrec')}>
                        {downloading === 'lrec' ? '⏳' : '⬇'} Recibidas
                    </button>
                </div>
            </div>

            {/* Modelo 347 txt + retenciones */}
            <div className="erp-card" style={{ marginBottom: '16px' }}>
                <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)' }}>
                    <div style={{ fontSize: '14px', fontWeight: 700 }}>347 · 111 · 190 · 130 · 200 · 202</div>
                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>Fase 0: exportes adicionales y esqueletos (validar siempre con asesoría / programa AEAT)</div>
                </div>
                <div style={{ padding: '12px 20px', display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                    <button className="btn btn-secondary btn-sm" disabled={downloading === '347txt'}
                        onClick={() => downloadFile(`/api/proxy/accounting/export/modelo347-aeat-txt?year=${selYear}`, `Modelo347_${selYear}_aeat.txt`, '347txt')}>
                        {downloading === '347txt' ? '⏳' : '⬇'} 347 TXT
                    </button>
                    <button className="btn btn-secondary btn-sm" disabled={downloading === 'm111'}
                        onClick={() => downloadJson(`/api/proxy/accounting/modelo-111?year=${selYear}&quarter=${selQ}`, `Modelo111_${selYear}_T${selQ}.json`, 'm111')}>
                        {downloading === 'm111' ? '⏳' : '⬇'} 111 JSON
                    </button>
                    <button className="btn btn-secondary btn-sm" disabled={downloading === 'm190'}
                        onClick={() => downloadJson(`/api/proxy/accounting/modelo-190?year=${selYear}`, `Modelo190_${selYear}.json`, 'm190')}>
                        {downloading === 'm190' ? '⏳' : '⬇'} 190 JSON
                    </button>
                    <button className="btn btn-secondary btn-sm" disabled={downloading === 'm130'}
                        onClick={() => downloadJson(`/api/proxy/accounting/modelo-130?year=${selYear}&quarter=${selQ}`, `Modelo130_${selYear}_T${selQ}.json`, 'm130')}>
                        {downloading === 'm130' ? '⏳' : '⬇'} 130 JSON
                    </button>
                    <button className="btn btn-primary btn-sm" disabled={downloading === 'm200'}
                        onClick={() => downloadFile(`/api/proxy/accounting/export/modelo200-xml?year=${selYear}`, `Modelo200_${selYear}_esqueleto.xml`, 'm200')}>
                        {downloading === 'm200' ? '⏳' : '⬇'} 200 XML
                    </button>
                    <button className="btn btn-primary btn-sm" disabled={downloading === 'm202'}
                        onClick={() => downloadFile(`/api/proxy/accounting/export/modelo202-xml?year=${selYear}&period=${(selQ - 1) * 3 + 1}`, `Modelo202_${selYear}_esqueleto.xml`, 'm202')}>
                        {downloading === 'm202' ? '⏳' : '⬇'} 202 XML
                    </button>
                </div>
            </div>

            {/* VIES */}
            <div style={{ marginBottom: '12px' }}>
                <h2 style={{ fontSize: '16px', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '4px' }}>
                    Validación VIES
                </h2>
                <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                    Verifica el NIF intracomunitario de un cliente o proveedor UE antes de emitir facturas exentas (art. 25 LIVA)
                </p>
            </div>
            <div className="erp-card" style={{ padding: '20px' }}>
                <div style={{ display: 'flex', gap: '10px', alignItems: 'flex-end', marginBottom: '16px', flexWrap: 'wrap' }}>
                    <div>
                        <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px', letterSpacing: '0.04em' }}>PAÍS (ISO-2)</label>
                        <input
                            value={viesCountry} onChange={e => setViesCountry(e.target.value.toUpperCase().slice(0, 2))}
                            placeholder="FR"
                            style={{ padding: '8px 10px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '13px', width: '60px', background: 'var(--bg-secondary)', color: 'var(--text-primary)', textAlign: 'center', letterSpacing: '0.1em', fontWeight: 700 }}
                        />
                    </div>
                    <div style={{ flex: 1, minWidth: '200px' }}>
                        <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px', letterSpacing: '0.04em' }}>NIF / VAT NUMBER (sin prefijo país)</label>
                        <input
                            value={viesVat} onChange={e => setViesVat(e.target.value.toUpperCase())}
                            placeholder="12345678901"
                            onKeyDown={e => e.key === 'Enter' && validateVies()}
                            style={{ padding: '8px 10px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '13px', width: '100%', background: 'var(--bg-secondary)', color: 'var(--text-primary)', boxSizing: 'border-box' }}
                        />
                    </div>
                    <button className="btn btn-primary" onClick={validateVies} disabled={viesLoading}
                        style={{ whiteSpace: 'nowrap' }}>
                        {viesLoading ? '⏳ Consultando...' : '🔍 Validar VIES'}
                    </button>
                </div>

                {viesResult && (
                    <div style={{
                        padding: '14px 16px', borderRadius: '8px',
                        background: viesResult.isValid ? 'var(--success-bg)' : 'var(--danger-bg)',
                        border: `1px solid ${viesResult.isValid ? '#a7f3d0' : '#fecaca'}`,
                    }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
                            <span style={{ fontSize: '18px' }}>{viesResult.isValid ? '✅' : '❌'}</span>
                            <span style={{ fontWeight: 700, fontSize: '14px', color: viesResult.isValid ? '#065f46' : '#991b1b' }}>
                                {viesResult.countryCode}{viesResult.vatNumber} — {viesResult.isValid ? 'NIF UE VÁLIDO' : 'NO VÁLIDO'}
                            </span>
                        </div>
                        {viesResult.name && (
                            <div style={{ fontSize: '13px', color: 'var(--text-secondary)', marginBottom: '4px' }}>
                                <strong>Razón social:</strong> {viesResult.name}
                            </div>
                        )}
                        {viesResult.address && (
                            <div style={{ fontSize: '13px', color: 'var(--text-secondary)', marginBottom: '4px' }}>
                                <strong>Dirección:</strong> {viesResult.address}
                            </div>
                        )}
                        <div style={{ fontSize: '12px', color: viesResult.isValid ? '#065f46' : '#991b1b', marginTop: '8px', fontStyle: 'italic' }}>
                            {viesResult.advice}
                        </div>
                        {viesResult.errorMessage && !viesResult.isValid && (
                            <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '6px' }}>
                                Detalle: {viesResult.errorMessage}
                            </div>
                        )}
                    </div>
                )}

                <div style={{ marginTop: '12px', fontSize: '11px', color: 'var(--text-muted)' }}>
                    Servicio VIES: VAT Information Exchange System (Reg. EU 904/2010) ·
                    Fuente: <em>ec.europa.eu/taxation_customs/vies</em>
                </div>
            </div>
        </PageContainer>
    );
}
